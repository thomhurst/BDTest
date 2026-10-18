using BDTest.Razor.Reports.Interfaces;

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
        var deleting = Path.Combine(_root, reportId + "-" + Guid.NewGuid().ToString("N") + ".deleting");
        // Claim the directory atomically so concurrent callers never delete the same files.
        try { Directory.Move(directory, deleting); }
        catch (DirectoryNotFoundException) { return Task.CompletedTask; }
        catch (IOException) when (!Directory.Exists(directory)) { return Task.CompletedTask; }
        try { Directory.Delete(deleting, recursive: true); }
        catch (DirectoryNotFoundException) { }
        catch (IOException) when (!Directory.Exists(deleting)) { }
        return Task.CompletedTask;
    }
}
