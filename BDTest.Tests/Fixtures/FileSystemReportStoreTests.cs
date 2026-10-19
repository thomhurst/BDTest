using System.Text;
using System.Diagnostics;
using BDTest.Razor.Reports.Storage;
using BDTest.Reports.Storage.FileSystem;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class FileSystemReportStoreTests
{
    private string _directory = null!; // Set before each test.

    [SetUp]
    public void SetUp() => _directory = Path.Combine(
        ResolvePhysicalDirectory(new DirectoryInfo(Path.GetTempPath())), "BDTest-file-store", Guid.NewGuid().ToString("N"));

    private static string ResolvePhysicalDirectory(DirectoryInfo directory)
    {
        // A system temp root may contain links (for example /var -> /private/var on macOS).
        // Resolve those only for the fixture; the store must still reject linked storage paths.
        if (directory.Parent == null) return directory.FullName;
        var physical = new DirectoryInfo(Path.Combine(ResolvePhysicalDirectory(directory.Parent), directory.Name));
        return physical.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? physical.FullName;
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private FileSystemReportStore Store() => new(Options.Create(new FileSystemReportStoreOptions
    {
        DirectoryPath = _directory, Retention = TimeSpan.FromDays(1)
    }));

    private static MemoryStream Bytes(string text) => new(Encoding.UTF8.GetBytes(text));

    private static async Task<string?> Read(IReportStore store, string id)
    {
        await using var stream = await store.OpenReadAsync(id, default);
        if (stream == null) return null;
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    [Test]
    public async Task Reports_preserve_bytes_position_and_case_across_store_instances()
    {
        IReportStore store = Store();
        using var input = Bytes("prefix  { \"unknown\": true } \n");
        input.Position = 6;
        await store.WriteAsync("CON", input, default);
        Assert.That(input.CanRead, Is.True);
        Assert.That(await Read(Store(), "CON"), Is.EqualTo("  { \"unknown\": true } \n"));
        using var lower = Bytes("lower");
        await store.WriteAsync("con", lower, default);
        Assert.That(await Read(store, "con"), Is.EqualTo("lower"));
        Assert.That(await Read(store, "CON"), Does.Contain("unknown"));
        Assert.That(await Read(store, "missing"), Is.Null);
        Assert.That(await store.DeleteAsync("CON", default), Is.True);
        Assert.That(await store.DeleteAsync("CON", default), Is.False);
        Assert.That(await Read(store, "con"), Is.EqualTo("lower"));
    }

    [Test]
    public async Task Index_replaces_filters_pages_and_deletes_without_reading_report_bytes()
    {
        var store = Store();
        foreach (var id in new[] { "d", "c", "b", "a" })
        {
            using var input = Bytes("not JSON");
            await store.WriteAsync(id, input, default);
            await store.UpsertAsync(new RunIndexEntry
            {
                RecordId = id, StartedAtDateTime = new DateTime(2026, 1, 1), BranchName = "main",
                Environment = "ci", Tag = "smoke", Counts = new RunIndexCounts { Passed = 3 }
            }, default);
        }
        RunIndexQuery Query(string? token = null) => new()
        {
            PageSize = 2, BranchName = "main", Environment = "ci", Tag = "smoke", ContinuationToken = token
        };
        var first = await Store().ListAsync(Query(), default);
        Assert.That(first.Entries.Select(entry => entry.RecordId), Is.EqualTo(new[] { "a", "b" }));
        Assert.That(first.Entries.All(entry => entry.Counts?.Passed == 3), Is.True);
        IRunIndexStore index = store;
        Assert.That(await index.DeleteAsync("b", default), Is.True);
        var second = await index.ListAsync(Query(first.ContinuationToken), default);
        Assert.That(second.Entries.Select(entry => entry.RecordId), Is.EqualTo(new[] { "c", "d" }));
        Assert.That(second.ContinuationToken, Is.Null);
        await index.UpsertAsync(new RunIndexEntry { RecordId = "a", BranchName = "other" }, default);
        Assert.That((await index.ListAsync(Query(), default)).Entries.Select(entry => entry.RecordId),
            Is.EqualTo(new[] { "c", "d" }));
        Assert.That(await index.DeleteAsync("b", default), Is.False);
        Assert.That(await Read(store, "b"), Is.EqualTo("not JSON"));
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await index.ListAsync(new RunIndexQuery { ContinuationToken = "invalid" }, default));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await index.ListAsync(new RunIndexQuery { PageSize = 0 }, default));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("../outside")]
    [TestCase("..\\outside")]
    [TestCase("/absolute")]
    [TestCase("C:\\absolute")]
    [TestCase("run:stream")]
    [TestCase("%2e%2e%2foutside")]
    [TestCase("run.")]
    [TestCase("run ")]
    [TestCase("é")]
    public async Task Every_ID_operation_rejects_unsafe_IDs_without_creating_storage(string? id)
    {
        var store = Store();
        using var input = Bytes("{}");
        await Assert.ThrowsAsync<ArgumentException>(async () => await store.WriteAsync(id!, input, default));
        await Assert.ThrowsAsync<ArgumentException>(async () => await store.OpenReadAsync(id!, default));
        await Assert.ThrowsAsync<ArgumentException>(async () => await ((IReportStore)store).DeleteAsync(id!, default));
        await Assert.ThrowsAsync<ArgumentException>(async () => await store.UpsertAsync(new() { RecordId = id }, default));
        await Assert.ThrowsAsync<ArgumentException>(async () => await ((IRunIndexStore)store).DeleteAsync(id!, default));
        Assert.That(Directory.Exists(_directory), Is.False);
    }

    [Test]
    public async Task IDs_have_a_bounded_length()
    {
        using var input = Bytes("{}");
        await Assert.ThrowsAsync<ArgumentException>(async () => await Store().WriteAsync(new string('a', 129), input, default));
        await Store().WriteAsync(new string('a', 128), input, default);
    }

    [Test]
    public async Task Precancelled_operations_do_not_create_storage()
    {
        var store = Store();
        var token = new CancellationToken(true);
        using var input = Bytes("{}");
        await Assert.CatchAsync<OperationCanceledException>(async () => await store.WriteAsync("run", input, token));
        await Assert.CatchAsync<OperationCanceledException>(async () => await store.OpenReadAsync("run", token));
        await Assert.CatchAsync<OperationCanceledException>(async () => await ((IReportStore)store).DeleteAsync("run", token));
        await Assert.CatchAsync<OperationCanceledException>(async () => await store.UpsertAsync(new() { RecordId = "run" }, token));
        await Assert.CatchAsync<OperationCanceledException>(async () => await store.ListAsync(new(), token));
        await Assert.CatchAsync<OperationCanceledException>(async () => await ((IRunIndexStore)store).DeleteAsync("run", token));
        Assert.That(Directory.Exists(_directory), Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Interrupted_write_preserves_old_report_and_removes_temporary_files(bool cancel)
    {
        var store = Store();
        using var original = Bytes("original");
        await store.WriteAsync("run", original, default);
        using var cancellation = new CancellationTokenSource();
        using var broken = new InterruptedStream(cancel ? cancellation : null);
        if (cancel)
            await Assert.CatchAsync<OperationCanceledException>(async () => await store.WriteAsync("run", broken, cancellation.Token));
        else
            await Assert.ThrowsAsync<IOException>(async () => await store.WriteAsync("run", broken, default));
        Assert.That(await Read(store, "run"), Is.EqualTo("original"));
        Assert.That(Directory.GetFiles(_directory, "*.tmp"), Is.Empty);
        Assert.That(broken.CanRead, Is.True);
    }

    [Test]
    public async Task Concurrent_instances_publish_complete_files_and_keep_open_readers_valid()
    {
        var store = Store();
        using var initial = Bytes("original");
        await store.WriteAsync("run", initial, default);
        await using var reader = await store.OpenReadAsync("run", default);
        var values = Enumerable.Range(0, 12).Select(i => new string((char)('a' + i), 100000)).ToArray();
        await Task.WhenAll(values.Select(async value =>
        {
            var other = Store();
            using var input = Bytes(value);
            await other.WriteAsync("run", input, default);
            await other.UpsertAsync(new() { RecordId = "run", Tag = value[..1] }, default);
            Assert.That(values, Does.Contain(await Read(other, "run")));
        }));
        Assert.That(values, Does.Contain(await Read(store, "run")));
        Assert.That((await store.ListAsync(new(), default)).Entries.Count, Is.EqualTo(1));
        using var oldReader = new StreamReader(Required(reader));
        Assert.That(await oldReader.ReadToEndAsync(), Is.EqualTo("original"));
        Assert.That(Directory.GetFiles(_directory, "*.tmp"), Is.Empty);
    }

    [Test]
    public async Task Separate_processes_wait_without_pruning_active_writes_and_can_cancel()
    {
        using var writer = StartWorker("writer");
        Process? contender = null;
        try
        {
            await Expect(writer, "holding");
            var temporary = Directory.GetFiles(_directory, "*.tmp").Single();
            contender = StartWorker("contender");
            await Expect(contender, "waiting-to-cancel");
            Assert.That(File.Exists(temporary), Is.True);
            await contender.StandardInput.WriteLineAsync("cancel");
            await Expect(contender, "cancelled");
            Assert.That(File.Exists(temporary), Is.True);
            await Expect(contender, "waiting-to-read");
            await writer.StandardInput.WriteLineAsync("release");
            await Expect(writer, "written");
            await Expect(contender, "complete:partial-complete");
            await writer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            await contender.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            Assert.That(writer.ExitCode, Is.Zero, await writer.StandardError.ReadToEndAsync());
            Assert.That(contender.ExitCode, Is.Zero, await contender.StandardError.ReadToEndAsync());
            Assert.That(Directory.GetFiles(_directory, "*.tmp"), Is.Empty);
        }
        finally
        {
            await StopWorker(writer);
            if (contender != null)
            {
                await StopWorker(contender);
                contender.Dispose();
            }
        }
    }

    private Process StartWorker(string mode)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.ArgumentList.Add(typeof(FileSystemReportStoreTests).Assembly.Location);
        start.ArgumentList.Add("file-store-worker");
        start.ArgumentList.Add(mode);
        start.ArgumentList.Add(_directory);
        return Process.Start(start) ?? throw new InvalidOperationException("Could not start storage worker.");
    }

    private static async Task Expect(Process process, string expected)
    {
        var actual = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30));
        Assert.That(actual, Is.EqualTo(expected), actual == null ? await process.StandardError.ReadToEndAsync() : null);
    }

    private static async Task StopWorker(Process process)
    {
        if (!process.HasExited) process.Kill(entireProcessTree: true);
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
    }

    [Test]
    public async Task Waiting_for_another_writer_can_be_cancelled()
    {
        var store = Store();
        await store.OpenReadAsync("missing", default);
        using var lease = new FileStream(Path.Combine(_directory, "store.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var cancellation = new CancellationTokenSource();
        var pending = store.ListAsync(new(), cancellation.Token);
        Assert.That(pending.IsCompleted, Is.False);
        cancellation.Cancel();
        await Assert.CatchAsync<OperationCanceledException>(async () => await pending);
    }

    [Test]
    public async Task Retention_prunes_expired_reports_indexes_and_abandoned_temporary_files()
    {
        var store = Store();
        using var original = Bytes("old");
        await store.WriteAsync("old", original, default);
        await store.UpsertAsync(new() { RecordId = "old" }, default);
        foreach (var path in Directory.GetFiles(_directory, "*.json"))
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-2));
        await File.WriteAllTextAsync(Path.Combine(_directory, "abandoned.tmp"), "partial");
        using var current = Bytes("current");
        await store.WriteAsync("current", current, default);
        await store.UpsertAsync(new() { RecordId = "current" }, default);
        Assert.That(await Read(store, "old"), Is.Null);
        Assert.That(await Read(store, "current"), Is.EqualTo("current"));
        Assert.That((await store.ListAsync(new(), default)).Entries.Select(entry => entry.RecordId), Is.EqualTo(new[] { "current" }));
        Assert.That(Directory.GetFiles(_directory, "*.tmp"), Is.Empty);
        // Read/list operations also enforce retention when no new reports arrive.
        foreach (var path in Directory.GetFiles(_directory, "*.json"))
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-2));
        Assert.That((await store.ListAsync(new(), default)).Entries, Is.Empty);
        Assert.That(await Read(store, "current"), Is.Null);
    }

    [Test]
    public async Task Replacement_renews_retention_and_delete_preserves_an_open_reader()
    {
        IReportStore store = Store();
        using var original = Bytes("original");
        await store.WriteAsync("run", original, default);
        var path = Directory.GetFiles(_directory, "report-*.json").Single();
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-2));
        using var replacement = Bytes("replacement");
        await store.WriteAsync("run", replacement, default);
        await using var stream = await store.OpenReadAsync("run", default);
        Assert.That(stream, Is.Not.Null);
        Assert.That(await store.DeleteAsync("run", default), Is.True);
        using var recreated = Bytes("recreated");
        await store.WriteAsync("run", recreated, default);
        using var reader = new StreamReader(Required(stream));
        Assert.That(await reader.ReadToEndAsync(), Is.EqualTo("replacement"));
        Assert.That(await Read(store, "run"), Is.EqualTo("recreated"));
    }

    [Test]
    public async Task Symbolic_link_files_cannot_escape_the_store()
    {
        var store = Store();
        using var input = Bytes("original");
        await store.WriteAsync("run", input, default);
        var path = Directory.GetFiles(_directory, "report-*.json").Single();
        var outside = Path.Combine(_directory, "untouched.txt");
        await File.WriteAllTextAsync(outside, "untouched");
        File.Delete(path);
        try { File.CreateSymbolicLink(path, outside); }
        catch (UnauthorizedAccessException) { Assert.Ignore("Creating symbolic links requires privileges on this host."); }
        catch (IOException exception) when ((exception.HResult & 0xffff) == 1314)
        { Assert.Ignore("Creating symbolic links requires privileges on this host."); }
        await Assert.ThrowsAsync<IOException>(async () => await store.OpenReadAsync("run", default));
        await Assert.ThrowsAsync<IOException>(async () => await store.WriteAsync("run", input, default));
        await Assert.ThrowsAsync<IOException>(async () => await ((IReportStore)store).DeleteAsync("run", default));
        Assert.That(await File.ReadAllTextAsync(outside), Is.EqualTo("untouched"));
    }

    [Test]
    public async Task Symbolic_link_ancestors_cannot_redirect_the_storage_directory()
    {
        Directory.CreateDirectory(_directory);
        var target = Path.Combine(_directory, "target");
        Directory.CreateDirectory(target);
        var link = Path.Combine(_directory, "link");
        try { Directory.CreateSymbolicLink(link, target); }
        catch (UnauthorizedAccessException) { Assert.Ignore("Creating symbolic links requires privileges on this host."); }
        catch (IOException exception) when ((exception.HResult & 0xffff) == 1314)
        { Assert.Ignore("Creating symbolic links requires privileges on this host."); }
        var store = new FileSystemReportStore(Options.Create(new FileSystemReportStoreOptions
        {
            DirectoryPath = Path.Combine(link, "reports")
        }));
        using var input = Bytes("data");
        await Assert.ThrowsAsync<IOException>(async () => await store.WriteAsync("run", input, default));
        Assert.That(Directory.GetFileSystemEntries(target), Is.Empty);
    }

    [Test]
    public async Task Registration_shares_one_store_and_validates_options_on_host_start()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddBDTestFileSystemStorage(options => options.DirectoryPath = _directory);
        using var host = builder.Build();
        await host.StartAsync();
        Assert.That(host.Services.GetRequiredService<IReportStore>(), Is.SameAs(host.Services.GetRequiredService<IRunIndexStore>()));
        await host.StopAsync();
        foreach (var options in new[]
        {
            new FileSystemReportStoreOptions(),
            new FileSystemReportStoreOptions { DirectoryPath = "relative" },
            new FileSystemReportStoreOptions { DirectoryPath = _directory, Retention = TimeSpan.Zero },
            new FileSystemReportStoreOptions { DirectoryPath = _directory, Retention = TimeSpan.MaxValue }
        })
        {
            var invalid = Host.CreateApplicationBuilder();
            invalid.Services.AddBDTestFileSystemStorage(value =>
            {
                value.DirectoryPath = options.DirectoryPath;
                value.Retention = options.Retention;
            });
            using var invalidHost = invalid.Build();
            await Assert.ThrowsAsync<OptionsValidationException>(async () => await invalidHost.StartAsync());
            Assert.Throws<OptionsValidationException>(() => new FileSystemReportStore(Options.Create(options)));
        }
    }

    private sealed class InterruptedStream(CancellationTokenSource? cancellation) : MemoryStream
    {
        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            await destination.WriteAsync(Encoding.UTF8.GetBytes("partial"), cancellationToken);
            if (cancellation != null)
            {
                cancellation.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
            }
            throw new IOException("Simulated input failure");
        }
    }
}
