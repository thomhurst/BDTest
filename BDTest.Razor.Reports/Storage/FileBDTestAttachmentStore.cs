using BDTest.Razor.Reports.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace BDTest.Razor.Reports.Storage;

/// <summary>Stores attachment bytes in a host-owned directory, using validated IDs rather than supplied file paths.</summary>
/// <remarks>The host owns the directory's permissions and lifetime. Do not expose it as static files or allow untrusted filesystem writers.</remarks>
public sealed class FileBDTestAttachmentStore : IBDTestAttachmentStore
{
    private readonly string _root;

    public FileBDTestAttachmentStore(string directory)
    {
        _root = Path.GetFullPath(directory ?? throw new ArgumentNullException(nameof(directory)));
        Directory.CreateDirectory(_root);
    }

    public Task<Stream> OpenReadAsync(string reportId, string attachmentId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AttachmentStorage.ValidateIds(reportId, attachmentId);
        ArgumentNullException.ThrowIfNull(attachmentId);
        try
        {
            return Task.FromResult<Stream>(new FileStream(Path.Combine(_root, reportId, attachmentId), FileMode.Open,
                FileAccess.Read, FileShare.Read | FileShare.Delete, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan));
        }
        catch (FileNotFoundException) { return Task.FromResult<Stream>(null); }
        catch (DirectoryNotFoundException) { return Task.FromResult<Stream>(null); }
    }

    public async Task WriteAsync(string reportId, string attachmentId, Stream content, long length, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AttachmentStorage.ValidateIds(reportId, attachmentId);
        ArgumentNullException.ThrowIfNull(attachmentId);
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        var directory = Path.Combine(_root, reportId);
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await using (var destination = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
                await AttachmentStorage.CopyExactlyAsync(content, destination, length, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, Path.Combine(directory, attachmentId), overwrite: true);
        }
        finally { File.Delete(temporary); }
    }

    public Task DeleteReportAsync(string reportId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AttachmentStorage.ValidateIds(reportId);
        var directory = Path.Combine(_root, reportId);
        var lockKey = OperatingSystem.IsWindows() ? directory.ToUpperInvariant() : directory;
        var lockName = "BDTest-attachment-delete-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(lockKey)));
        // Serialize cleanup across store instances/processes, including recovery of earlier failed claims.
        using var mutex = new Mutex(false, lockName);
        try
        {
            // WaitOne also supports named mutexes on Unix; check cancellation while waiting.
            while (!mutex.WaitOne(100))
                cancellationToken.ThrowIfCancellationRequested();
        }
        catch (AbandonedMutexException) { } // Ownership passes to us when a previous process exits.
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            DeleteReportContent(reportId, directory, cancellationToken);
        }
        finally { mutex.ReleaseMutex(); }
        return Task.CompletedTask;
    }

    private void DeleteReportContent(string reportId, string directory, CancellationToken cancellationToken)
    {
        var deleting = Path.Combine(_root, reportId + "-" + Guid.NewGuid().ToString("N") + ".deleting");
        // Hide the report directory atomically before removing its content.
        try { Directory.Move(directory, deleting); }
        catch (DirectoryNotFoundException) { }
        catch (IOException) when (!Directory.Exists(directory)) { }

        // Discover failed claims from earlier calls, including calls in another server instance.
        string[] pending;
        try { pending = Directory.GetDirectories(_root, reportId + "-*.deleting"); }
        catch (DirectoryNotFoundException) { return; }
        foreach (var claimed in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { Directory.Delete(claimed, recursive: true); }
            catch (DirectoryNotFoundException) { }
            catch (IOException) when (!Directory.Exists(claimed)) { }
        }
    }
}
