using System.Text;
using BDTest.Helpers;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class AttachmentTests
{
    [Test]
    public async Task Setup_steps_hooks_and_teardown_keep_their_owners()
    {
        using var run = BDTestRun.Start();
        var fixture = new Capture(run, "test") { CaptureHooks = true };
        fixture.Attachments.Attach("setup.txt", new byte[] { 1 }, "text/plain");
        var scenario = await fixture.When(() => fixture.Attach("step.txt")).Then(() => Done()).BDTestAsync();
        fixture.BDTestContext.Attachments.Attach("teardown.txt", new byte[] { 2 }, "text/plain");

        Assert.That(scenario.Attachments.Select(file => file.Name), Is.EqualTo(new[] { "setup.txt", "teardown.txt" }));
        Assert.That(scenario.Steps[0].Attachments.Select(file => file.Name),
            Is.EqualTo(new[] { "before.txt", "step.txt", "after.txt" }));
        Assert.That(scenario.Steps[1].Attachments.Select(file => file.Name), Is.EqualTo(new[] { "before.txt", "after.txt" }));
        var attachment = scenario.Steps[0].Attachments[1];
        using var content = run.OpenAttachment(attachment.Id);
        Assert.That(new StreamReader(content).ReadToEnd(), Is.EqualTo("step.txt"));
        Assert.That(attachment.Length, Is.EqualTo(8));
        Assert.That(attachment.AttemptNumber, Is.EqualTo(1));
        var restoredScenario = BDTestJsonHelper.Deserialize(BDTestJsonHelper.GetTestJsonData(null, run)).Scenarios.Single();
        Assert.That(restoredScenario.Attachments.Select(file => file.Name), Is.EqualTo(new[] { "setup.txt", "teardown.txt" }));
    }

    [Test]
    public async Task Retry_files_keep_distinct_attempt_identity_and_content()
    {
        var options = new BDTestOptions();
        options.RetryTestRules.Add(_ => true, retryLimit: 1);
        using var run = BDTestRun.Start(options);
        var fixture = new Capture(run, "test");
        var attempts = 0;
        var scenario = await fixture.When(() =>
        {
            fixture.Attach("attempt-" + ++attempts);
            if (attempts == 1) throw new InvalidOperationException("retry");
        }).Then(() => Done()).BDTestAsync();

        Assert.That(scenario.Steps[0].Attachments.Select(file => file.AttemptNumber), Is.EqualTo(new[] { 1, 2 }));
        Assert.That(scenario.Steps[0].Attachments.Select(file => file.Id).Distinct().Count(), Is.EqualTo(2));
        foreach (var file in scenario.Steps[0].Attachments)
        {
            using var reader = new StreamReader(run.OpenAttachment(file.Id));
            Assert.That(reader.ReadToEnd(), Is.EqualTo("attempt-" + file.AttemptNumber));
        }
    }

    [Test]
    public async Task Metadata_round_trips_without_local_paths_or_file_content()
    {
        using var run = BDTestRun.Start();
        var fixture = new Capture(run, "test");
        await fixture.When(() => fixture.Attach("private-file-content")).Then(() => Done()).BDTestAsync();
        var json = BDTestJsonHelper.GetTestJsonData(null, run);
        var restored = BDTestJsonHelper.Deserialize(json).Scenarios.Single().Steps[0].Attachments.Single();
        Assert.That(restored.Name, Is.EqualTo("private-file-content"));
        Assert.That(restored.MimeType, Is.EqualTo("text/plain"));
        Assert.That(restored.AttemptNumber, Is.EqualTo(1));
        Assert.That(restored.Uri, Is.Null);
        Assert.That(json, Does.Not.Contain(run.AttachmentFiles.DirectoryPath));
        Assert.That(json, Does.Not.Contain(Convert.ToBase64String(Encoding.UTF8.GetBytes("private-file-content"))));
        var historical = BDTestJsonHelper.Deserialize("{\"Scenarios\":[{\"Steps\":[{}]}]}");
        Assert.That(historical.Scenarios[0].Attachments, Is.Empty);
        Assert.That(historical.Scenarios[0].Steps[0].Attachments, Is.Empty);
    }

    [Test]
    public async Task Parallel_runs_with_identical_test_ids_keep_files_and_cleanup_separate()
    {
        using var first = BDTestRun.Start();
        using var second = BDTestRun.Start();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = 0;
        async Task<Scenario> Execute(BDTestRun run, string text)
        {
            var fixture = new Capture(run, "same-id");
            return await fixture.When(async () =>
            {
                if (Interlocked.Increment(ref entered) == 2) ready.SetResult();
                await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
                fixture.Attach(text);
            }).Then(() => Done()).BDTestAsync();
        }
        var results = await Task.WhenAll(Task.Run(() => Execute(first, "first")), Task.Run(() => Execute(second, "second")));
        var firstFile = results[0].Steps[0].Attachments.Single();
        var secondFile = results[1].Steps[0].Attachments.Single();
        Assert.Throws<KeyNotFoundException>(() => first.OpenAttachment(secondFile.Id));
        Assert.Throws<KeyNotFoundException>(() => second.OpenAttachment(firstFile.Id));
        first.Dispose();
        Assert.That(Directory.Exists(first.AttachmentFiles.DirectoryPath), Is.False);
        using var reader = new StreamReader(second.OpenAttachment(secondFile.Id));
        Assert.That(reader.ReadToEnd(), Is.EqualTo("second"));
        var json = BDTestJsonHelper.GetTestJsonData(null, second);
        Assert.That(json, Does.Contain(secondFile.Id).And.Not.Contain(firstFile.Id));
    }

    [Test]
    public async Task Parallel_tests_in_one_run_keep_step_owners()
    {
        using var run = BDTestRun.Start();
        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(index => Task.Run(async () =>
        {
            var fixture = new Capture(run, index.ToString());
            return await fixture.When(async () =>
            {
                await Task.Yield();
                fixture.Attach(index.ToString());
            }).Then(() => Done()).BDTestAsync();
        })));
        foreach (var scenario in results)
            Assert.That(scenario.Steps[0].Attachments.Single().Name, Is.EqualTo(scenario.FrameworkTestId));
    }

    [Test]
    public async Task Outline_factory_files_belong_to_each_row_even_when_factory_fails()
    {
        using var run = BDTestRun.Start();
        var fixture = new Capture(run, "outline");
        var builder = fixture.ScenarioOutline("row <Value>", new[] { new { Value = 1 }, new { Value = 2 } }, row =>
        {
            fixture.Attach("factory-" + row.Value);
            if (row.Value == 1) throw new InvalidOperationException("factory failure");
            return fixture.When(() => fixture.Attach("step-" + row.Value)).Then(() => Done());
        });
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await builder.BDTestAsync());
        var outline = run.Scenarios.Single();
        Assert.That(outline.Attachments, Is.Empty);
        Assert.That(outline.Examples.Select(row => row.Attachments.Single().Name), Is.EqualTo(new[] { "factory-1", "factory-2" }));
        Assert.That(outline.Examples[1].Steps[0].Attachments.Single().Name, Is.EqualTo("step-2"));
    }

    [Test]
    public async Task Limits_are_frozen_and_failed_copies_release_quota_and_partial_files()
    {
        var options = new BDTestOptions();
        options.Attachments.MaximumAttachmentBytes = 3;
        options.Attachments.MaximumRunBytes = 5;
        using var run = BDTestRun.Start(options);
        options.Attachments.MaximumAttachmentBytes = 100;
        options.Attachments.MaximumRunBytes = 100;
        var fixture = new Capture(run, "test");
        Assert.Throws<IOException>(() => fixture.Attach("four"));
        using var broken = new BrokenStream();
        Assert.Throws<IOException>(() => fixture.Attachments.Attach("broken", broken, "text/plain"));
        Assert.That(broken.CanRead, Is.True, "The caller owns the source stream.");
        Assert.That(Directory.GetFiles(run.AttachmentFiles.DirectoryPath), Is.Empty);
        fixture.Attach("one");
        Assert.Throws<IOException>(() => fixture.Attach("two"));
        fixture.Attach("ok");
        var scenario = await fixture.When(() => Done()).Then(() => Done()).BDTestAsync();
        Assert.That(scenario.Attachments.Select(file => file.Length), Is.EqualTo(new long[] { 3, 2 }));
        Assert.That(Directory.GetFiles(run.AttachmentFiles.DirectoryPath), Has.Length.EqualTo(2));
    }

    [Test]
    public async Task Concurrent_copies_cannot_exceed_run_quota()
    {
        var options = new BDTestOptions();
        options.Attachments.MaximumRunBytes = 3;
        using var run = BDTestRun.Start(options);
        var fixture = new Capture(run, "test");
        var successes = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Task.Run(() =>
        {
            try { fixture.Attach("abc"); return 1; }
            catch (IOException) { return 0; }
        })));
        Assert.That(successes.Sum(), Is.EqualTo(1));
        var scenario = await fixture.When(() => Done()).Then(() => Done()).BDTestAsync();
        Assert.That(scenario.Attachments, Has.Count.EqualTo(1));
        Assert.That(Directory.GetFiles(run.AttachmentFiles.DirectoryPath), Has.Length.EqualTo(1));
    }

    [Test]
    public async Task Streams_copy_from_current_position_and_file_capture_keeps_only_basename()
    {
        using var run = BDTestRun.Start();
        var fixture = new Capture(run, "test");
        using var source = new MemoryStream(new byte[] { 1, 2, 3 });
        source.Position = 1;
        fixture.Attachments.Attach("stream", source, "application/octet-stream");
        Assert.That(source.CanRead, Is.True);
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, new byte[] { 4 });
            fixture.Attachments.AttachFile(path);
            var scenario = await fixture.When(() => Done()).Then(() => Done()).BDTestAsync();
            var files = scenario.Attachments;
            Assert.That(files[0].Length, Is.EqualTo(2));
            using var captured = run.OpenAttachment(files[0].Id);
            Assert.That(captured.ReadByte(), Is.EqualTo(2));
            Assert.That(files[1].Name, Is.EqualTo(Path.GetFileName(path)));
            Assert.That(files[1].MimeType, Is.EqualTo("application/octet-stream"));
        }
        finally { File.Delete(path); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Clear_and_dispose_delete_files_after_failed_execution(bool dispose)
    {
        using var run = BDTestRun.Start();
        var fixture = new Capture(run, "test");
        var savedWriter = fixture.Attachments;
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await fixture.When(() =>
        {
            fixture.Attach("before-failure");
            throw new InvalidOperationException("failed");
        }).Then(() => Done()).BDTestAsync());
        var file = run.Scenarios.Single().Steps[0].Attachments.Single();
        Assert.That(Directory.Exists(run.AttachmentFiles.DirectoryPath), Is.True);
        if (dispose) run.Dispose();
        else BDTestUtil.ClearScenarios(run);
        Assert.That(Directory.Exists(run.AttachmentFiles.DirectoryPath), Is.False);
        if (dispose)
        {
            Assert.Throws<ObjectDisposedException>(() => run.OpenAttachment(file.Id));
            Assert.Throws<ObjectDisposedException>(() => savedWriter.Attach("late", new byte[] { 1 }, "text/plain"));
        }
        else
        {
            Assert.Throws<KeyNotFoundException>(() => run.OpenAttachment(file.Id));
            fixture.Attach("fresh");
            var next = await fixture.When(() => Done()).Then(() => Done()).BDTestAsync();
            Assert.That(next.Attachments.Single().Name, Is.EqualTo("fresh"));
        }
    }

    [Test]
    public async Task Abandoned_timeout_tasks_cannot_attach_to_completed_steps_or_later_attempts()
    {
        using var run = BDTestRun.Start();
        var fixture = new Capture(run, "test");
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task LateStep()
        {
            await finish.Task;
            try { fixture.Attach("late"); result.SetResult(null); }
            catch (Exception exception) { result.SetResult(exception); }
        }
        try
        {
            await Assert.ThrowsAsync<TimeoutException>(async () => await fixture.When(() => LateStep())
                .WithStepTimeout(TimeSpan.FromMilliseconds(100)).Then(() => Done()).BDTestAsync().WaitAsync(TimeSpan.FromSeconds(10)));
        }
        finally { finish.TrySetResult(); }
        Assert.That(await result.Task.WaitAsync(TimeSpan.FromSeconds(10)), Is.TypeOf<InvalidOperationException>());
        Assert.That(run.Scenarios.Single().Steps[0].Attachments, Is.Empty);
        Assert.That(Directory.Exists(run.AttachmentFiles.DirectoryPath), Is.False);
    }

    [TestCase("")]
    [TestCase("not a mime type")]
    [TestCase("text/*")]
    [TestCase("text/plain\r\nx-header: value")]
    public void Invalid_metadata_is_rejected_before_creating_files(string mimeType)
    {
        using var run = BDTestRun.Start();
        var writer = new Capture(run, "test").Attachments;
        Assert.Throws<ArgumentException>(() => writer.Attach("name", Array.Empty<byte>(), mimeType));
        Assert.Throws<ArgumentException>(() => writer.Attach(" ", Array.Empty<byte>(), "text/plain"));
        Assert.Throws<ArgumentNullException>(() => writer.Attach("name", (byte[])null, "text/plain"));
        Assert.That(Directory.Exists(run.AttachmentFiles.DirectoryPath), Is.False);
    }

    [Test]
    public void Negative_limits_are_rejected_at_start()
    {
        var options = new BDTestOptions();
        options.Attachments.MaximumAttachmentBytes = -1;
        Assert.Throws<ArgumentOutOfRangeException>(() => BDTestRun.Start(options));
        options.Attachments.MaximumAttachmentBytes = 1;
        options.Attachments.MaximumRunBytes = -1;
        Assert.Throws<ArgumentOutOfRangeException>(() => BDTestRun.Start(options));
    }

    [Test]
    public async Task Failed_steps_keep_after_hook_files_and_do_not_assign_files_to_unreached_steps()
    {
        using var run = BDTestRun.Start();
        var fixture = new Capture(run, "test") { CaptureHooks = true };
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await fixture.When(() =>
        {
            fixture.Attach("failure");
            throw new InvalidOperationException("step failed");
        }).Then(() => Done()).BDTestAsync());
        var scenario = run.Scenarios.Single();
        Assert.That(scenario.Steps[0].Attachments.Select(file => file.Name),
            Is.EqualTo(new[] { "before.txt", "failure", "after.txt" }));
        Assert.That(scenario.Steps[1].Attachments, Is.Empty);
    }

    [Test]
    public async Task A_saved_writer_keeps_its_run_when_another_run_is_current()
    {
        using var first = BDTestRun.Start();
        using var second = BDTestRun.Start();
        var firstFixture = new Capture(first, "same-id");
        var firstScenario = await firstFixture.When(() => Done()).Then(() => Done()).BDTestAsync();
        var saved = firstFixture.Attachments;
        var secondFixture = new Capture(second, "same-id");
        var secondScenario = await secondFixture.When(() => saved.Attach("saved", new byte[] { 1 }, "text/plain"))
            .Then(() => Done()).BDTestAsync();
        Assert.That(firstScenario.Attachments.Single().Name, Is.EqualTo("saved"));
        Assert.That(secondScenario.Attachments, Is.Empty);
        Assert.That(secondScenario.Steps.SelectMany(step => step.Attachments), Is.Empty);
    }

    [Test]
    public async Task Retry_hooks_capture_on_the_scenario_for_the_upcoming_attempt()
    {
        var options = new BDTestOptions();
        options.RetryTestRules.Add(_ => true, retryLimit: 1);
        using var run = BDTestRun.Start(options);
        var fixture = new Capture(run, "test") { CaptureRetryHook = true };
        var attempts = 0;
        var scenario = await fixture.When(() =>
        {
            if (++attempts == 1) throw new InvalidOperationException("retry");
        }).Then(() => Done()).BDTestAsync();
        Assert.That(scenario.Attachments.Single().Name, Is.EqualTo("retry-hook"));
        Assert.That(scenario.Attachments.Single().AttemptNumber, Is.EqualTo(2));
    }

    [Test]
    public async Task Abandoned_factory_tasks_cannot_add_files_to_a_completed_row()
    {
        using var run = BDTestRun.Start();
        var fixture = new Capture(run, "test");
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<Exception> late = null;
        var outline = await fixture.ScenarioOutline("row <Value>", new[] { new { Value = 1 } }, _ =>
        {
            late = Task.Run(async () =>
            {
                await finish.Task;
                try { fixture.Attach("late"); return null; }
                catch (Exception exception) { return exception; }
            });
            return fixture.When(() => Done()).Then(() => Done());
        }).BDTestAsync();
        finish.SetResult();
        Assert.That(await late.WaitAsync(TimeSpan.FromSeconds(10)), Is.TypeOf<InvalidOperationException>());
        Assert.That(outline.Examples.Single().Attachments, Is.Empty);
        Assert.That(Directory.Exists(run.AttachmentFiles.DirectoryPath), Is.False);
    }

    [Test]
    public async Task Zero_limits_allow_empty_content_without_allocating_quota()
    {
        var options = new BDTestOptions();
        options.Attachments.MaximumAttachmentBytes = 0;
        options.Attachments.MaximumRunBytes = 0;
        using var run = BDTestRun.Start(options);
        var fixture = new Capture(run, "test");
        fixture.Attachments.Attach("empty", Array.Empty<byte>(), "text/plain");
        Assert.Throws<IOException>(() => fixture.Attach("nonempty"));
        var scenario = await fixture.When(() => Done()).Then(() => Done()).BDTestAsync();
        Assert.That(scenario.Attachments.Single().Length, Is.Zero);
        using var stream = run.OpenAttachment(scenario.Attachments.Single().Id);
        Assert.That(stream.ReadByte(), Is.EqualTo(-1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Completing_setup_releases_only_attachments_without_a_scenario(bool createScenario)
    {
        var options = new BDTestOptions();
        options.Attachments.MaximumRunBytes = 3;
        using var run = BDTestRun.Start(options);
        var fixture = new Capture(run, "setup");
        fixture.Start();
        fixture.Attach("abc");
        var path = Directory.GetFiles(run.AttachmentFiles.DirectoryPath).Single();
        var attachmentId = Path.GetFileName(path);
        if (createScenario)
            await fixture.When(() => Done()).Then(() => Done()).BDTestAsync();
        fixture.Finish();
        fixture.Finish();

        Assert.That(File.Exists(path), Is.EqualTo(createScenario));
        if (createScenario)
        {
            using var content = run.OpenAttachment(attachmentId);
            Assert.That(new StreamReader(content).ReadToEnd(), Is.EqualTo("abc"));
            Assert.Throws<IOException>(() => new Capture(run, "next").Attach("abc"));
        }
        else
        {
            Assert.Throws<KeyNotFoundException>(() => run.OpenAttachment(attachmentId));
            var next = new Capture(run, "next");
            next.Attach("abc");
            var scenario = await next.When(() => Done()).Then(() => Done()).BDTestAsync();
            Assert.That(scenario.Attachments.Single().Name, Is.EqualTo("abc"));
        }
    }

    [Test]
    public async Task Background_setup_and_outline_factory_captures_keep_every_attachment()
    {
        using var run = BDTestRun.Start();
        var fixture = new Capture(run, "outline");
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var background = Task.Run(async () =>
        {
            await start.Task;
            for (var index = 0; index < 1000; index++) fixture.Attach("background-" + index);
        });
        var outline = await fixture.ScenarioOutline("row <Value>", Enumerable.Range(0, 100).Select(value => new { Value = value }), row =>
        {
            fixture.Attach("factory-" + row.Value);
            return fixture.When(async () =>
            {
                start.TrySetResult();
                await Task.Yield();
            }).Then(() => Done());
        }).BDTestAsync();
        await background.WaitAsync(TimeSpan.FromSeconds(10));

        var files = outline.Attachments.Concat(outline.Examples.SelectMany(row => row.Attachments)).ToArray();
        Assert.That(files, Has.Length.EqualTo(1100));
        Assert.That(files.Select(file => file.Name).Distinct().Count(), Is.EqualTo(1100));
        foreach (var row in outline.Examples)
            Assert.That(row.Attachments.Any(file => file.Name == "factory-" + row.ExampleValues["Value"]), Is.True);
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task Setup_capture_finishing_after_teardown_releases_files_and_quota(bool useContext, bool createScenario)
    {
        var options = new BDTestOptions();
        options.Attachments.MaximumRunBytes = 3;
        using var run = BDTestRun.Start(options);
        var fixture = new Capture(run, "setup");
        fixture.Start();
        var writer = useContext ? fixture.BDTestContext.Attachments : fixture.Attachments;
        using var source = new PausedStream();
        var capture = Task.Run(() =>
        {
            try { writer.Attach("late", source, "text/plain"); return null; }
            catch (Exception exception) { return exception; }
        });
        try
        {
            await source.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            if (createScenario)
                await fixture.When(() => Done()).Then(() => Done()).BDTestAsync();
            fixture.Finish();
        }
        finally { source.Resume.Set(); }

        Assert.That(await capture.WaitAsync(TimeSpan.FromSeconds(10)), Is.TypeOf<InvalidOperationException>());
        Assert.That(Directory.GetFiles(run.AttachmentFiles.DirectoryPath), Is.Empty);
        Assert.That(run.Scenarios.SelectMany(scenario => scenario.Attachments), Is.Empty);
        var next = new Capture(run, "next");
        next.Attach("abc");
        var nextScenario = await next.When(() => Done()).Then(() => Done()).BDTestAsync();
        Assert.That(nextScenario.Attachments.Single().Length, Is.EqualTo(3));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Background_setup_cannot_start_capture_after_teardown(bool useContext)
    {
        using var run = BDTestRun.Start();
        var fixture = new Capture(run, "setup");
        fixture.Start();
        var context = fixture.BDTestContext;
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var capture = Task.Run(async () =>
        {
            await finish.Task;
            var writer = useContext ? context.Attachments : fixture.Attachments;
            try { writer.Attach("late", new byte[] { 1 }, "text/plain"); return null; }
            catch (Exception exception) { return exception; }
        });
        fixture.Finish();
        finish.SetResult();
        Assert.That(await capture.WaitAsync(TimeSpan.FromSeconds(10)), Is.TypeOf<InvalidOperationException>());
        Assert.That(Directory.Exists(run.AttachmentFiles.DirectoryPath), Is.False);
    }

    private sealed class PausedStream : MemoryStream
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal ManualResetEventSlim Resume { get; } = new();

        public override int Read(byte[] buffer, int offset, int count)
        {
            Started.TrySetResult();
            if (!Resume.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Capture was not resumed.");
            return base.Read(buffer, offset, count);
        }

        internal PausedStream() : base(new byte[] { 1, 2, 3 }) { }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Resume.Dispose();
            base.Dispose(disposing);
        }
    }

    private sealed class BrokenStream : MemoryStream
    {
        private bool _read;
        public override bool CanSeek => false;
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_read) throw new IOException("source failed");
            _read = true;
            buffer[offset] = 42;
            return 1;
        }
    }

    private static void Done() { }
    public sealed class Context { }
    private sealed class Capture(BDTestRun run, string id) : AbstractContextBDTestBase<Context>
    {
        public override BDTestRun TestRun => run;
        protected override string BDTestExecutionId => id;
        public void Start() => BeginTest(id);
        public void Finish() => EndTest();
        public bool CaptureHooks { get; init; }
        public bool CaptureRetryHook { get; init; }
        public void Attach(string name) => Attachments.Attach(name, Encoding.UTF8.GetBytes(name), "text/plain");
        public override Task OnBeforeStep(Step step)
        {
            if (CaptureHooks) Attach("before.txt");
            return Task.CompletedTask;
        }
        public override Task OnAfterStep(Step step)
        {
            if (CaptureHooks) Attach("after.txt");
            return Task.CompletedTask;
        }
        public override Task OnBeforeRetry()
        {
            if (CaptureRetryHook) Attach("retry-hook");
            return Task.CompletedTask;
        }
    }
}
