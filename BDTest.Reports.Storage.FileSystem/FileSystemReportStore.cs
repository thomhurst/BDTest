using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BDTest.Razor.Reports.Storage;
using Microsoft.Extensions.Options;

namespace BDTest.Reports.Storage.FileSystem;

/// <summary>Stores report bytes and index metadata in a dedicated local directory.</summary>
public sealed class FileSystemReportStore : IReportStore, IRunIndexStore
{
    private readonly string _directory;
    private readonly TimeSpan _retention;

    public FileSystemReportStore(IOptions<FileSystemReportStoreOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var value = options.Value;
        if (!FileSystemReportStoreOptions.IsValid(value))
            throw new OptionsValidationException(Options.DefaultName, typeof(FileSystemReportStoreOptions),
                ["Specify an absolute DirectoryPath and a Retention greater than zero and no more than 36500 days."]);
        _directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(value.DirectoryPath));
        _retention = value.Retention;
    }

    public async Task<Stream?> OpenReadAsync(string reportId, CancellationToken cancellationToken)
    {
        var path = ItemPath(reportId, "report");
        using var lease = await AcquireAsync(cancellationToken).ConfigureAwait(false);
        Prune(cancellationToken);
        return OpenRead(path);
    }

    public async Task WriteAsync(string reportId, Stream utf8Json, CancellationToken cancellationToken)
    {
        var path = ItemPath(reportId, "report");
        ArgumentNullException.ThrowIfNull(utf8Json);
        using var lease = await AcquireAsync(cancellationToken).ConfigureAwait(false);
        await WriteAtomicallyAsync(path, (output, token) => utf8Json.CopyToAsync(output, token), cancellationToken)
            .ConfigureAwait(false);
        Prune(cancellationToken);
    }

    public async Task UpsertAsync(RunIndexEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var path = ItemPath(entry.RecordId, "index");
        using var lease = await AcquireAsync(cancellationToken).ConfigureAwait(false);
        await WriteAtomicallyAsync(path, (output, token) => JsonSerializer.SerializeAsync(output, entry,
            cancellationToken: token), cancellationToken).ConfigureAwait(false);
        Prune(cancellationToken);
    }

    public async Task<RunIndexPage> ListAsync(RunIndexQuery query, CancellationToken cancellationToken)
    {
        // Validate the query before doing I/O or retention work.
        RunIndexPaging.Apply([], query);
        using var lease = await AcquireAsync(cancellationToken).ConfigureAwait(false);
        Prune(cancellationToken);
        var entries = new List<RunIndexEntry>();
        foreach (var path in Directory.EnumerateFiles(_directory, "index-*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var input = OpenRead(path);
            if (input == null) continue;
            var entry = await JsonSerializer.DeserializeAsync<RunIndexEntry>(input, cancellationToken: cancellationToken)
                .ConfigureAwait(false) ?? throw new InvalidDataException($"Null index entry in {path}.");
            if (!string.Equals(ItemPath(entry.RecordId, "index"), path, StringComparison.Ordinal))
                throw new InvalidDataException($"Index entry does not match its filename: {path}.");
            entries.Add(entry);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return RunIndexPaging.Apply(entries, query);
    }

    Task<bool> IReportStore.DeleteAsync(string reportId, CancellationToken cancellationToken) =>
        DeleteAsync(ItemPath(reportId, "report"), cancellationToken);

    Task<bool> IRunIndexStore.DeleteAsync(string reportId, CancellationToken cancellationToken) =>
        DeleteAsync(ItemPath(reportId, "index"), cancellationToken);

    private async Task<bool> DeleteAsync(string path, CancellationToken cancellationToken)
    {
        using var lease = await AcquireAsync(cancellationToken).ConfigureAwait(false);
        RejectLink(path);
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    private async Task WriteAtomicallyAsync(string path, Func<Stream, CancellationToken, Task> write,
        CancellationToken cancellationToken)
    {
        var temporary = ContainedPath($"{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 81920, FileOptions.Asynchronous))
            {
                await write(output, cancellationToken).ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            cancellationToken.ThrowIfCancellationRequested();
            RejectLink(path);
            if (File.Exists(path)) File.Replace(temporary, path, destinationBackupFileName: null);
            else File.Move(temporary, path);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private FileStream? OpenRead(string path)
    {
        RejectLink(path);
        try
        {
            // Existing readers may finish while another operation replaces or deletes the file.
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
                81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        }
        catch (FileNotFoundException) { return null; }
    }

    private async Task<FileStream> AcquireAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        for (var directory = new DirectoryInfo(_directory); directory != null; directory = directory.Parent)
            RejectLink(directory.FullName);
        Directory.CreateDirectory(_directory);
        var path = ContainedPath("store.lock");
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RejectLink(path);
            try
            {
                // Cooperating store instances/processes serialize mutations and retention scans.
                // Keep this file after closing: deleting it would let two processes lock different files.
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            // Windows uses HRESULT sharing/lock violations; Unix preserves the native errno.
            // EWOULDBLOCK/EAGAIN is 35 on Darwin/FreeBSD and 11 on Linux.
            catch (IOException exception) when (OperatingSystem.IsWindows()
                ? (exception.HResult & 0xffff) is 32 or 33
                : exception.HResult == (OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD() ? 35 : 11))
            {
                await Task.Delay(25, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private void Prune(CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow - _retention;
        foreach (var path in Directory.EnumerateFiles(_directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = Path.GetFileName(path);
            var stored = (name.StartsWith("report-", StringComparison.Ordinal) || name.StartsWith("index-", StringComparison.Ordinal))
                && name.EndsWith(".json", StringComparison.Ordinal);
            // No other cooperating writer can own a temporary file while we hold the lease.
            if (!stored && !name.EndsWith(".tmp", StringComparison.Ordinal)) continue;
            RejectLink(path);
            if (!stored || File.GetLastWriteTimeUtc(path) <= cutoff) File.Delete(path);
        }
    }

    private string ItemPath(string? reportId, string kind)
    {
        if (string.IsNullOrEmpty(reportId) || reportId.Length > 128 ||
            reportId.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
            throw new ArgumentException("Report IDs must contain 1–128 ASCII letters, digits, hyphens or underscores.", nameof(reportId));
        // Hashing also preserves case-sensitive IDs on case-insensitive file systems.
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(reportId)));
        return ContainedPath($"{kind}-{key}.json");
    }

    private string ContainedPath(string name)
    {
        var path = Path.GetFullPath(Path.Combine(_directory, name));
        if (!string.Equals(Path.GetDirectoryName(path), _directory,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new ArgumentException("Storage path must remain inside the configured directory.", nameof(name));
        return path;
    }

    private static void RejectLink(string path)
    {
        try
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"Storage paths cannot be symbolic links or reparse points: {path}");
        }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
    }
}
