using System.IO;
using BDTest.Razor.Reports.Storage;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

public class FileAttachmentStoreTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Deletion_recovers_all_pending_directories_for_only_the_selected_report(bool currentContentExists)
    {
        var directory = Path.Combine(Path.GetTempPath(), "BDTest-file-tests", Guid.NewGuid().ToString("N"));
        var report = Guid.NewGuid().ToString("N");
        var otherReport = Guid.NewGuid().ToString("N");
        try
        {
            var store = new FileBDTestAttachmentStore(directory);
            foreach (var id in new[] { report, report, otherReport })
            {
                var pending = Path.Combine(directory, id + "-" + Guid.NewGuid().ToString("N") + ".deleting");
                Directory.CreateDirectory(pending);
                await File.WriteAllTextAsync(Path.Combine(pending, Guid.NewGuid().ToString("N")), "remaining");
            }
            if (currentContentExists)
            {
                using var input = new MemoryStream(new byte[] { 1 });
                await store.WriteAsync(report, Guid.NewGuid().ToString("N"), input, 1);
            }

            await store.DeleteReportAsync(report);
            var remaining = Directory.GetDirectories(directory);

            await Assert.That((remaining).Length).IsEqualTo(1);
            await Assert.That(Path.GetFileName(remaining.Single())).StartsWith(otherReport + "-");

            await Assert.That((Directory.GetFiles(remaining.Single())).Length).IsEqualTo(1);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test]
    [RunOn(global::TUnit.Core.Enums.OS.Windows)]
    public async Task Failed_deletion_is_retried_by_a_new_store_instance()
    {
        var directory = Path.Combine(Path.GetTempPath(), "BDTest-file-tests", Guid.NewGuid().ToString("N"));
        var report = Guid.NewGuid().ToString("N");
        var attachment = Guid.NewGuid().ToString("N");
        try
        {
            var store = new FileBDTestAttachmentStore(directory);
            using var input = new MemoryStream(new byte[] { 1 });
            await store.WriteAsync(report, attachment, input, 1);
            File.SetAttributes(Path.Combine(directory, report, attachment), FileAttributes.ReadOnly);

            await Assert.That(() => store.DeleteReportAsync(report)).Throws<UnauthorizedAccessException>();
            await Assert.That(Directory.Exists(Path.Combine(directory, report))).IsFalse();
            var remaining = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);

            await Assert.That((remaining).Length).IsEqualTo(1);
            await Assert.That(() =>
                new FileBDTestAttachmentStore(directory).DeleteReportAsync(report)).Throws<UnauthorizedAccessException>();
            File.SetAttributes(remaining.Single(), FileAttributes.Normal);

            await new FileBDTestAttachmentStore(directory).DeleteReportAsync(report);
            await Assert.That(Directory.GetFileSystemEntries(directory)).IsEmpty();
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
                    File.SetAttributes(file, FileAttributes.Normal);
                Directory.Delete(directory, true);
            }
        }
    }

    [Test]
    public async Task Concurrent_deletion_and_missing_content_are_successful_no_ops()
    {
        var directory = Path.Combine(Path.GetTempPath(), "BDTest-file-tests", Guid.NewGuid().ToString("N"));
        var report = Guid.NewGuid().ToString("N");
        try
        {
            for (var attempt = 0; attempt < 10; attempt++)
            {
                var store = new FileBDTestAttachmentStore(directory);
                using var input = new MemoryStream(new byte[] { 1 });
                await store.WriteAsync(report, Guid.NewGuid().ToString("N"), input, 1);
                await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
                    new FileBDTestAttachmentStore(directory).DeleteReportAsync(report))));
                await store.DeleteReportAsync(report);
                await Assert.That(Directory.Exists(Path.Combine(directory, report))).IsFalse();
                await Assert.That(Directory.GetFileSystemEntries(directory)).IsEmpty();
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test]
    [Arguments(1)]
    [Arguments(3)]
    public async Task Invalid_length_preserves_previous_content_and_removes_partial_file(int length)
    {
        var directory = Path.Combine(Path.GetTempPath(), "BDTest-file-tests", Guid.NewGuid().ToString("N"));
        var report = Guid.NewGuid().ToString("N");
        var attachment = Guid.NewGuid().ToString("N");
        try
        {
            var store = new FileBDTestAttachmentStore(directory);
            using var original = new MemoryStream(new byte[] { 4, 5 });
            await store.WriteAsync(report, attachment, original, 2);
            using var replacement = new MemoryStream(new byte[] { 7, 8 });
            await Assert.That(() => store.WriteAsync(report, attachment, replacement, length)).ThrowsExactly<InvalidDataException>();
            await Assert.That(replacement.CanRead).IsTrue();
            await Assert.That(original.CanRead).IsTrue();
            using var read = await store.OpenReadAsync(report, attachment);
            await Assert.That(Required(read).ReadByte()).IsEqualTo(4);

            await Assert.That((Directory.GetFiles(directory, "*", SearchOption.AllDirectories)).Length).IsEqualTo(1);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Test]
    [Arguments("../outside")]
    [Arguments("..")]
    [Arguments("C:\\outside")]
    [Arguments("invalid/id")]
    public async Task Invalid_ids_never_create_files(string id)
    {
        var directory = Path.Combine(Path.GetTempPath(), "BDTest-file-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileBDTestAttachmentStore(directory);
            using var input = new MemoryStream(new byte[] { 1 });
            await Assert.That(() => store.WriteAsync(id, Guid.NewGuid().ToString("N"), input, 1)).ThrowsExactly<ArgumentException>();
            await Assert.That(() => store.WriteAsync(Guid.NewGuid().ToString("N"), id, input, 1)).ThrowsExactly<ArgumentException>();
            await Assert.That(Directory.GetFiles(directory, "*", SearchOption.AllDirectories)).IsEmpty();
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Failed_or_cancelled_copy_removes_partial_content(bool cancel)
    {
        var directory = Path.Combine(Path.GetTempPath(), "BDTest-file-tests", Guid.NewGuid().ToString("N"));
        var report = Guid.NewGuid().ToString("N");
        var attachment = Guid.NewGuid().ToString("N");
        try
        {
            var store = new FileBDTestAttachmentStore(directory);
            using var cancellation = new CancellationTokenSource();
            using var input = new FailedCopyStream(cancellation, cancel);
            if (cancel)
                await Assert.That(() => store.WriteAsync(report, attachment, input, 2, cancellation.Token)).Throws<OperationCanceledException>();
            else
                await Assert.That(() => store.WriteAsync(report, attachment, input, 2, cancellation.Token)).ThrowsExactly<IOException>();
            await Assert.That(input.CanRead).IsTrue();
            await Assert.That(await store.OpenReadAsync(report, attachment)).IsNull();
            await Assert.That(Directory.GetFiles(directory, "*", SearchOption.AllDirectories)).IsEmpty();
        }
        finally { Directory.Delete(directory, true); }
    }

    private sealed class FailedCopyStream(CancellationTokenSource cancellation, bool cancel) : MemoryStream(new byte[] { 1, 2 })
    {
        private bool _read;
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_read) throw new IOException("source failed");
            _read = true;
            buffer.Span[0] = 1;
            if (cancel) cancellation.Cancel();
            return ValueTask.FromResult(1);
        }
    }
}
