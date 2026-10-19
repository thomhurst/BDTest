using System.IO;
using System.Text;
using System.Diagnostics;
using BDTest.Razor.Reports.Storage;
using BDTest.Reports.Storage.FileSystem;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

public class FileSystemReportStoreTests
{
    private string _directory = null!; // Set before each test.

    [Before(HookType.Test)]
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

    [After(HookType.Test)]
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
        await Assert.That(input.CanRead).IsTrue();
        await Assert.That(await Read(Store(), "CON")).IsEqualTo("  { \"unknown\": true } \n");
        using var lower = Bytes("lower");
        await store.WriteAsync("con", lower, default);
        await Assert.That(await Read(store, "con")).IsEqualTo("lower");
        await Assert.That(await Read(store, "CON")).Contains("unknown");
        await Assert.That(await Read(store, "missing")).IsNull();
        await Assert.That(await store.DeleteAsync("CON", default)).IsTrue();
        await Assert.That(await store.DeleteAsync("CON", default)).IsFalse();
        await Assert.That(await Read(store, "con")).IsEqualTo("lower");
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
        await Assert.That(first.Entries.Select(entry => entry.RecordId)).IsEquivalentTo((new[] { "a", "b" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(first.Entries.All(entry => entry.Counts?.Passed == 3)).IsTrue();
        IRunIndexStore index = store;
        await Assert.That(await index.DeleteAsync("b", default)).IsTrue();
        var second = await index.ListAsync(Query(first.ContinuationToken), default);
        await Assert.That(second.Entries.Select(entry => entry.RecordId)).IsEquivalentTo((new[] { "c", "d" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(second.ContinuationToken).IsNull();
        await index.UpsertAsync(new RunIndexEntry { RecordId = "a", BranchName = "other" }, default);
        await Assert.That((await index.ListAsync(Query(), default)).Entries.Select(entry => entry.RecordId)).IsEquivalentTo((new[] { "c", "d" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(await index.DeleteAsync("b", default)).IsFalse();
        await Assert.That(await Read(store, "b")).IsEqualTo("not JSON");
        await Assert.That(async () =>
            await index.ListAsync(new RunIndexQuery { ContinuationToken = "invalid" }, default)).ThrowsExactly<ArgumentException>();
        await Assert.That(async () =>
            await index.ListAsync(new RunIndexQuery { PageSize = 0 }, default)).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("../outside")]
    [Arguments("..\\outside")]
    [Arguments("/absolute")]
    [Arguments("C:\\absolute")]
    [Arguments("run:stream")]
    [Arguments("%2e%2e%2foutside")]
    [Arguments("run.")]
    [Arguments("run ")]
    [Arguments("é")]
    public async Task Every_ID_operation_rejects_unsafe_IDs_without_creating_storage(string? id)
    {
        var store = Store();
        using var input = Bytes("{}");
        await Assert.That(async () => await store.WriteAsync(id!, input, default)).ThrowsExactly<ArgumentException>();
        await Assert.That(async () => await store.OpenReadAsync(id!, default)).ThrowsExactly<ArgumentException>();
        await Assert.That(async () => await ((IReportStore)store).DeleteAsync(id!, default)).ThrowsExactly<ArgumentException>();
        await Assert.That(async () => await store.UpsertAsync(new() { RecordId = id }, default)).ThrowsExactly<ArgumentException>();
        await Assert.That(async () => await ((IRunIndexStore)store).DeleteAsync(id!, default)).ThrowsExactly<ArgumentException>();
        await Assert.That(Directory.Exists(_directory)).IsFalse();
    }

    [Test]
    public async Task IDs_have_a_bounded_length()
    {
        using var input = Bytes("{}");
        await Assert.That(async () => await Store().WriteAsync(new string('a', 129), input, default)).ThrowsExactly<ArgumentException>();
        await Store().WriteAsync(new string('a', 128), input, default);
    }

    [Test]
    public async Task Precancelled_operations_do_not_create_storage()
    {
        var store = Store();
        var token = new CancellationToken(true);
        using var input = Bytes("{}");
        await Assert.That(async () => await store.WriteAsync("run", input, token)).Throws<OperationCanceledException>();
        await Assert.That(async () => await store.OpenReadAsync("run", token)).Throws<OperationCanceledException>();
        await Assert.That(async () => await ((IReportStore)store).DeleteAsync("run", token)).Throws<OperationCanceledException>();
        await Assert.That(async () => await store.UpsertAsync(new() { RecordId = "run" }, token)).Throws<OperationCanceledException>();
        await Assert.That(async () => await store.ListAsync(new(), token)).Throws<OperationCanceledException>();
        await Assert.That(async () => await ((IRunIndexStore)store).DeleteAsync("run", token)).Throws<OperationCanceledException>();
        await Assert.That(Directory.Exists(_directory)).IsFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Interrupted_write_preserves_old_report_and_removes_temporary_files(bool cancel)
    {
        var store = Store();
        using var original = Bytes("original");
        await store.WriteAsync("run", original, default);
        using var cancellation = new CancellationTokenSource();
        using var broken = new InterruptedStream(cancel ? cancellation : null);
        if (cancel)
            await Assert.That(async () => await store.WriteAsync("run", broken, cancellation.Token)).Throws<OperationCanceledException>();
        else
            await Assert.That(async () => await store.WriteAsync("run", broken, default)).ThrowsExactly<IOException>();
        await Assert.That(await Read(store, "run")).IsEqualTo("original");
        await Assert.That(Directory.GetFiles(_directory, "*.tmp")).IsEmpty();
        await Assert.That(broken.CanRead).IsTrue();
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
            await Assert.That(values).Contains(Required(await Read(other, "run")));
        }));
        await Assert.That(values).Contains(Required(await Read(store, "run")));
        await Assert.That((await store.ListAsync(new(), default)).Entries.Count).IsEqualTo(1);
        using var oldReader = new StreamReader(Required(reader));
        await Assert.That(await oldReader.ReadToEndAsync()).IsEqualTo("original");
        await Assert.That(Directory.GetFiles(_directory, "*.tmp")).IsEmpty();
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
            await Assert.That(File.Exists(temporary)).IsTrue();
            await contender.StandardInput.WriteLineAsync("cancel");
            await Expect(contender, "cancelled");
            await Assert.That(File.Exists(temporary)).IsTrue();
            await Expect(contender, "waiting-to-read");
            await writer.StandardInput.WriteLineAsync("release");
            await Expect(writer, "written");
            await Expect(contender, "complete:partial-complete");
            await writer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            await contender.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            await Assert.That(writer.ExitCode).IsZero().Because(await writer.StandardError.ReadToEndAsync());
            await Assert.That(contender.ExitCode).IsZero().Because(await contender.StandardError.ReadToEndAsync());
            await Assert.That(Directory.GetFiles(_directory, "*.tmp")).IsEmpty();
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
        await Assert.That(actual).IsEqualTo(expected).Because(actual == null ? await process.StandardError.ReadToEndAsync() : "The worker must emit the expected protocol line");
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
        await Assert.That(pending.IsCompleted).IsFalse();
        cancellation.Cancel();
        await Assert.That(async () => await pending).Throws<OperationCanceledException>();
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
        await Assert.That(await Read(store, "old")).IsNull();
        await Assert.That(await Read(store, "current")).IsEqualTo("current");
        await Assert.That((await store.ListAsync(new(), default)).Entries.Select(entry => entry.RecordId)).IsEquivalentTo((new[] { "current" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(Directory.GetFiles(_directory, "*.tmp")).IsEmpty();
        // Read/list operations also enforce retention when no new reports arrive.
        foreach (var path in Directory.GetFiles(_directory, "*.json"))
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-2));
        await Assert.That((await store.ListAsync(new(), default)).Entries).IsEmpty();
        await Assert.That(await Read(store, "current")).IsNull();
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
        await Assert.That(stream).IsNotNull();
        await Assert.That(await store.DeleteAsync("run", default)).IsTrue();
        using var recreated = Bytes("recreated");
        await store.WriteAsync("run", recreated, default);
        using var reader = new StreamReader(Required(stream));
        await Assert.That(await reader.ReadToEndAsync()).IsEqualTo("replacement");
        await Assert.That(await Read(store, "run")).IsEqualTo("recreated");
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
        catch (UnauthorizedAccessException) { Skip.Test("Creating symbolic links requires privileges on this host."); }
        catch (IOException exception) when ((exception.HResult & 0xffff) == 1314)
        { Skip.Test("Creating symbolic links requires privileges on this host."); }
        await Assert.That(async () => await store.OpenReadAsync("run", default)).ThrowsExactly<IOException>();
        await Assert.That(async () => await store.WriteAsync("run", input, default)).ThrowsExactly<IOException>();
        await Assert.That(async () => await ((IReportStore)store).DeleteAsync("run", default)).ThrowsExactly<IOException>();
        await Assert.That(await File.ReadAllTextAsync(outside)).IsEqualTo("untouched");
    }

    [Test]
    public async Task Symbolic_link_ancestors_cannot_redirect_the_storage_directory()
    {
        Directory.CreateDirectory(_directory);
        var target = Path.Combine(_directory, "target");
        Directory.CreateDirectory(target);
        var link = Path.Combine(_directory, "link");
        try { Directory.CreateSymbolicLink(link, target); }
        catch (UnauthorizedAccessException) { Skip.Test("Creating symbolic links requires privileges on this host."); }
        catch (IOException exception) when ((exception.HResult & 0xffff) == 1314)
        { Skip.Test("Creating symbolic links requires privileges on this host."); }
        var store = new FileSystemReportStore(Options.Create(new FileSystemReportStoreOptions
        {
            DirectoryPath = Path.Combine(link, "reports")
        }));
        using var input = Bytes("data");
        await Assert.That(async () => await store.WriteAsync("run", input, default)).ThrowsExactly<IOException>();
        await Assert.That(Directory.GetFileSystemEntries(target)).IsEmpty();
    }

    [Test]
    public async Task Registration_shares_one_store_and_validates_options_on_host_start()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddBDTestFileSystemStorage(options => options.DirectoryPath = _directory);
        using var host = builder.Build();
        await host.StartAsync();
        await Assert.That(host.Services.GetRequiredService<IReportStore>()).IsSameReferenceAs(host.Services.GetRequiredService<IRunIndexStore>());
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
            await Assert.That(async () => await invalidHost.StartAsync()).ThrowsExactly<OptionsValidationException>();
            await Assert.That(() => new FileSystemReportStore(Options.Create(options))).ThrowsExactly<OptionsValidationException>();
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
