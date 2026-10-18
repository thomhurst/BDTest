using BDTest.Razor.Reports.Storage;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class FileAttachmentStoreTests
{
    [TestCase(false)]
    [TestCase(true)]
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
            Assert.That(remaining, Has.Length.EqualTo(1));
            Assert.That(Path.GetFileName(remaining.Single()), Does.StartWith(otherReport + "-"));
            Assert.That(Directory.GetFiles(remaining.Single()), Has.Length.EqualTo(1));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test]
    [Platform("Win")]
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

            await Assert.CatchAsync<UnauthorizedAccessException>(() => store.DeleteReportAsync(report));
            Assert.That(Directory.Exists(Path.Combine(directory, report)), Is.False);
            var remaining = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
            Assert.That(remaining, Has.Length.EqualTo(1));
            await Assert.CatchAsync<UnauthorizedAccessException>(() =>
                new FileBDTestAttachmentStore(directory).DeleteReportAsync(report));
            File.SetAttributes(remaining.Single(), FileAttributes.Normal);

            await new FileBDTestAttachmentStore(directory).DeleteReportAsync(report);
            Assert.That(Directory.GetFileSystemEntries(directory), Is.Empty);
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
                Assert.That(Directory.Exists(Path.Combine(directory, report)), Is.False);
                Assert.That(Directory.GetFileSystemEntries(directory), Is.Empty);
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    [TestCase(1)]
    [TestCase(3)]
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
            await Assert.ThrowsAsync<InvalidDataException>(() => store.WriteAsync(report, attachment, replacement, length));
            Assert.That(replacement.CanRead, Is.True);
            Assert.That(original.CanRead, Is.True);
            using var read = await store.OpenReadAsync(report, attachment);
            Assert.That(Required(read).ReadByte(), Is.EqualTo(4));
            Assert.That(Directory.GetFiles(directory, "*", SearchOption.AllDirectories), Has.Length.EqualTo(1));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [TestCase("../outside")]
    [TestCase("..")]
    [TestCase("C:\\outside")]
    [TestCase("invalid/id")]
    public async Task Invalid_ids_never_create_files(string id)
    {
        var directory = Path.Combine(Path.GetTempPath(), "BDTest-file-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileBDTestAttachmentStore(directory);
            using var input = new MemoryStream(new byte[] { 1 });
            await Assert.ThrowsAsync<ArgumentException>(() => store.WriteAsync(id, Guid.NewGuid().ToString("N"), input, 1));
            await Assert.ThrowsAsync<ArgumentException>(() => store.WriteAsync(Guid.NewGuid().ToString("N"), id, input, 1));
            Assert.That(Directory.GetFiles(directory, "*", SearchOption.AllDirectories), Is.Empty);
        }
        finally { Directory.Delete(directory, true); }
    }

    [TestCase(false)]
    [TestCase(true)]
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
                await Assert.CatchAsync<OperationCanceledException>(() => store.WriteAsync(report, attachment, input, 2, cancellation.Token));
            else
                await Assert.ThrowsAsync<IOException>(() => store.WriteAsync(report, attachment, input, 2, cancellation.Token));
            Assert.That(input.CanRead, Is.True);
            Assert.That(await store.OpenReadAsync(report, attachment), Is.Null);
            Assert.That(Directory.GetFiles(directory, "*", SearchOption.AllDirectories), Is.Empty);
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
