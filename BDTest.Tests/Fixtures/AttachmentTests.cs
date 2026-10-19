using System.IO;
using System.Text;
using BDTest.Helpers;
using BDTest.Test;
using System.Threading.Tasks;

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

        await Assert.That(Required(scenario.Attachments).Select(file => file.Name)).IsEquivalentTo((new[] { "setup.txt", "teardown.txt" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(Required(Required(scenario.Steps)[0].Attachments).Select(file => file.Name)).IsEquivalentTo((new[] { "before.txt", "step.txt", "after.txt" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(Required(scenario.Steps[1].Attachments).Select(file => file.Name)).IsEquivalentTo((new[] { "before.txt", "after.txt" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        var attachment = Required(scenario.Steps[0].Attachments)[1];
        using var content = run.OpenAttachment(Required(attachment.Id));
        await Assert.That(new StreamReader(content).ReadToEnd()).IsEqualTo("step.txt");
        await Assert.That(attachment.Length).IsEqualTo(8);
        await Assert.That(attachment.AttemptNumber).IsEqualTo(1);
        var restoredScenario = Required(Required(BDTestJsonHelper.Deserialize(BDTestJsonHelper.GetTestJsonData(null, run))).Scenarios).Single();
        await Assert.That(Required(restoredScenario.Attachments).Select(file => file.Name)).IsEquivalentTo((new[] { "setup.txt", "teardown.txt" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
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

        await Assert.That(Required(Required(scenario.Steps)[0].Attachments).Select(file => file.AttemptNumber)).IsEquivalentTo(new[] { 1, 2 }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(Required(scenario.Steps[0].Attachments).Select(file => file.Id).Distinct().Count()).IsEqualTo(2);
        foreach (var file in Required(scenario.Steps[0].Attachments))
        {
            using var reader = new StreamReader(run.OpenAttachment(Required(file.Id)));
            await Assert.That(reader.ReadToEnd()).IsEqualTo("attempt-" + file.AttemptNumber);
        }
    }

    [Test]
    public async Task Metadata_round_trips_without_local_paths_or_file_content()
    {
        using var run = BDTestRun.Start();
        var fixture = new Capture(run, "test");
        await fixture.When(() => fixture.Attach("private-file-content")).Then(() => Done()).BDTestAsync();
        var json = BDTestJsonHelper.GetTestJsonData(null, run);
        var restored = Required(Required(Required(Required(BDTestJsonHelper.Deserialize(json)).Scenarios).Single().Steps)[0].Attachments).Single();
        await Assert.That(restored.Name).IsEqualTo("private-file-content");
        await Assert.That(restored.MimeType).IsEqualTo("text/plain");
        await Assert.That(restored.AttemptNumber).IsEqualTo(1);
        await Assert.That(restored.Uri).IsNull();
        await Assert.That(json).DoesNotContain(run.AttachmentFiles.DirectoryPath);
        await Assert.That(json).DoesNotContain(Convert.ToBase64String(Encoding.UTF8.GetBytes("private-file-content")));
        var historical = Required(BDTestJsonHelper.Deserialize("{\"Scenarios\":[{\"Steps\":[{}]}]}"));
        await Assert.That(Required(historical.Scenarios)[0].Attachments).IsEmpty();
        await Assert.That(Required(historical.Scenarios[0].Steps)[0].Attachments).IsEmpty();
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
        var firstFile = Required(Required(results[0].Steps)[0].Attachments).Single();
        var secondFile = Required(Required(results[1].Steps)[0].Attachments).Single();
        await Assert.That(() => first.OpenAttachment(Required(secondFile.Id))).ThrowsExactly<KeyNotFoundException>();
        await Assert.That(() => second.OpenAttachment(Required(firstFile.Id))).ThrowsExactly<KeyNotFoundException>();
        first.Dispose();
        await Assert.That(Directory.Exists(first.AttachmentFiles.DirectoryPath)).IsFalse();
        using var reader = new StreamReader(second.OpenAttachment(Required(secondFile.Id)));
        await Assert.That(reader.ReadToEnd()).IsEqualTo("second");
        var json = BDTestJsonHelper.GetTestJsonData(null, second);

        await Assert.That(json).Contains(secondFile.Id).And.DoesNotContain(Required(firstFile.Id));
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
            await Assert.That(Required(Required(scenario.Steps)[0].Attachments).Single().Name).IsEqualTo(scenario.FrameworkTestId);
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
        await Assert.That(async () => await builder.BDTestAsync()).ThrowsExactly<InvalidOperationException>();
        var outline = run.Scenarios.Single();
        await Assert.That(outline.Attachments).IsEmpty();
        await Assert.That(Required(outline.Examples).Select(row => Required(row.Attachments).Single().Name)).IsEquivalentTo((new[] { "factory-1", "factory-2" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(Required(Required(outline.Examples[1].Steps)[0].Attachments).Single().Name).IsEqualTo("step-2");
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
        await Assert.That(() => fixture.Attach("four")).ThrowsExactly<IOException>();
        using var broken = new BrokenStream();
        await Assert.That(() => fixture.Attachments.Attach("broken", broken, "text/plain")).ThrowsExactly<IOException>();
        await Assert.That(broken.CanRead).IsTrue().Because("The caller owns the source stream.");
        await Assert.That(Directory.GetFiles(run.AttachmentFiles.DirectoryPath)).IsEmpty();
        fixture.Attach("one");
        await Assert.That(() => fixture.Attach("two")).ThrowsExactly<IOException>();
        fixture.Attach("ok");
        var scenario = await fixture.When(() => Done()).Then(() => Done()).BDTestAsync();
        await Assert.That(Required(scenario.Attachments).Select(file => file.Length)).IsEquivalentTo(new long[] { 3, 2 }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);

        await Assert.That((Directory.GetFiles(run.AttachmentFiles.DirectoryPath)).Length).IsEqualTo(2);
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
        await Assert.That(successes.Sum()).IsEqualTo(1);
        var scenario = await fixture.When(() => Done()).Then(() => Done()).BDTestAsync();
        await Assert.That(scenario.Attachments).Count().IsEqualTo(1);

        await Assert.That((Directory.GetFiles(run.AttachmentFiles.DirectoryPath)).Length).IsEqualTo(1);
    }

    [Test]
    public async Task Streams_copy_from_current_position_and_file_capture_keeps_only_basename()
    {
        using var run = BDTestRun.Start();
        var fixture = new Capture(run, "test");
        using var source = new MemoryStream(new byte[] { 1, 2, 3 });
        source.Position = 1;
        fixture.Attachments.Attach("stream", source, "application/octet-stream");
        await Assert.That(source.CanRead).IsTrue();
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, new byte[] { 4 });
            fixture.Attachments.AttachFile(path);
            var scenario = await fixture.When(() => Done()).Then(() => Done()).BDTestAsync();
            var files = Required(scenario.Attachments);
            await Assert.That(files[0].Length).IsEqualTo(2);
            using var captured = run.OpenAttachment(Required(files[0].Id));
            await Assert.That(captured.ReadByte()).IsEqualTo(2);
            await Assert.That(files[1].Name).IsEqualTo(Path.GetFileName(path));
            await Assert.That(files[1].MimeType).IsEqualTo("application/octet-stream");
        }
        finally { File.Delete(path); }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Clear_and_dispose_delete_files_after_failed_execution(bool dispose)
    {
        using var run = BDTestRun.Start();
        var fixture = new Capture(run, "test");
        var savedWriter = fixture.Attachments;
        await Assert.That(async () => await fixture.When(() =>
        {
            fixture.Attach("before-failure");
            throw new InvalidOperationException("failed");
        }).Then(() => Done()).BDTestAsync()).ThrowsExactly<InvalidOperationException>();
        var file = Required(Required(run.Scenarios.Single().Steps)[0].Attachments).Single();
        await Assert.That(Directory.Exists(run.AttachmentFiles.DirectoryPath)).IsTrue();
        if (dispose) run.Dispose();
        else BDTestUtil.ClearScenarios(run);
        await Assert.That(Directory.Exists(run.AttachmentFiles.DirectoryPath)).IsFalse();
        if (dispose)
        {
            await Assert.That(() => run.OpenAttachment(Required(file.Id))).ThrowsExactly<ObjectDisposedException>();
            await Assert.That(() => savedWriter.Attach("late", new byte[] { 1 }, "text/plain")).ThrowsExactly<ObjectDisposedException>();
        }
        else
        {
            await Assert.That(() => run.OpenAttachment(Required(file.Id))).ThrowsExactly<KeyNotFoundException>();
            fixture.Attach("fresh");
            var next = await fixture.When(() => Done()).Then(() => Done()).BDTestAsync();
            await Assert.That(Required(next.Attachments).Single().Name).IsEqualTo("fresh");
        }
    }

    [Test]
    public async Task Abandoned_timeout_tasks_cannot_attach_to_completed_steps_or_later_attempts()
    {
        using var run = BDTestRun.Start();
        var fixture = new Capture(run, "test");
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task LateStep()
        {
            await finish.Task;
            try { fixture.Attach("late"); result.SetResult(null); }
            catch (Exception exception) { result.SetResult(exception); }
        }
        try
        {
            await Assert.That(async () => await fixture.When(() => LateStep())
                .WithStepTimeout(TimeSpan.FromMilliseconds(100)).Then(() => Done()).BDTestAsync().WaitAsync(TimeSpan.FromSeconds(10))).ThrowsExactly<TimeoutException>();
        }
        finally { finish.TrySetResult(); }
        await Assert.That(await result.Task.WaitAsync(TimeSpan.FromSeconds(10))).IsTypeOf<InvalidOperationException>();
        await Assert.That(Required(run.Scenarios.Single().Steps)[0].Attachments).IsEmpty();
        await Assert.That(Directory.Exists(run.AttachmentFiles.DirectoryPath)).IsFalse();
    }

    [Test]
    [Arguments("")]
    [Arguments("not a mime type")]
    [Arguments("text/*")]
    [Arguments("text/plain\r\nx-header: value")]
    public async Task Invalid_metadata_is_rejected_before_creating_files(string mimeType)
    {
        using var run = BDTestRun.Start();
        var writer = new Capture(run, "test").Attachments;
        await Assert.That(() => writer.Attach("name", Array.Empty<byte>(), mimeType)).ThrowsExactly<ArgumentException>();
        await Assert.That(() => writer.Attach(" ", Array.Empty<byte>(), "text/plain")).ThrowsExactly<ArgumentException>();
        await Assert.That(() => writer.Attach("name", (byte[])null!, "text/plain")).ThrowsExactly<ArgumentNullException>();
        await Assert.That(Directory.Exists(run.AttachmentFiles.DirectoryPath)).IsFalse();
    }

    [Test]
    [Arguments("screenshots/login.png")]
    [Arguments("screenshots\\login.png")]
    [Arguments("line\nfeed")]
    [Arguments("control\u0001")]
    public async Task Invalid_names_fail_at_capture_before_creating_files(string name)
    {
        using var run = BDTestRun.Start();
        var writer = new Capture(run, "test").Attachments;
        await Assert.That(() => writer.Attach(name, Array.Empty<byte>(), "text/plain")).ThrowsExactly<ArgumentException>();
        await Assert.That(Directory.Exists(run.AttachmentFiles.DirectoryPath)).IsFalse();
    }

    [Test]
    public async Task Capture_accepts_metadata_boundaries_and_rejects_longer_values()
    {
        using var run = BDTestRun.Start();
        var fixture = new Capture(run, "test");
        var mime = "application/" + new string('a', 244);
        await Assert.That(() => fixture.Attachments.Attach(new string('a', 256), Array.Empty<byte>(), "text/plain")).ThrowsExactly<ArgumentException>();
        await Assert.That(() => fixture.Attachments.Attach("name", Array.Empty<byte>(), mime + "a")).ThrowsExactly<ArgumentException>();
        await Assert.That(() => fixture.Attachments.Attach("name", Array.Empty<byte>(), "text/plain;p=" + new string('a', 243))).ThrowsExactly<ArgumentException>();
        await Assert.That(Directory.Exists(run.AttachmentFiles.DirectoryPath)).IsFalse();
        fixture.Attachments.Attach(new string('a', 255), Array.Empty<byte>(), mime);
        var scenario = await fixture.When(() => Done()).Then(() => Done()).BDTestAsync();

        await Assert.That(Required(Required(scenario.Attachments).Single().Name).Length).IsEqualTo(255);

        await Assert.That(Required(Required(scenario.Attachments).Single().MimeType).Length).IsEqualTo(256);
    }

    [Test]
    public async Task Negative_limits_are_rejected_at_start()
    {
        var options = new BDTestOptions();
        options.Attachments.MaximumAttachmentBytes = -1;
        await Assert.That(() => BDTestRun.Start(options)).ThrowsExactly<ArgumentOutOfRangeException>();
        options.Attachments.MaximumAttachmentBytes = 1;
        options.Attachments.MaximumRunBytes = -1;
        await Assert.That(() => BDTestRun.Start(options)).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Failed_steps_keep_after_hook_files_and_do_not_assign_files_to_unreached_steps()
    {
        using var run = BDTestRun.Start();
        var fixture = new Capture(run, "test") { CaptureHooks = true };
        await Assert.That(async () => await fixture.When(() =>
        {
            fixture.Attach("failure");
            throw new InvalidOperationException("step failed");
        }).Then(() => Done()).BDTestAsync()).ThrowsExactly<InvalidOperationException>();
        var scenario = run.Scenarios.Single();
        await Assert.That(Required(Required(scenario.Steps)[0].Attachments).Select(file => file.Name)).IsEquivalentTo((new[] { "before.txt", "failure", "after.txt" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(scenario.Steps[1].Attachments).IsEmpty();
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
        await Assert.That(Required(firstScenario.Attachments).Single().Name).IsEqualTo("saved");
        await Assert.That(secondScenario.Attachments).IsEmpty();
        await Assert.That(Required(secondScenario.Steps).SelectMany(step => Required(step.Attachments))).IsEmpty();
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
        await Assert.That(Required(scenario.Attachments).Single().Name).IsEqualTo("retry-hook");
        await Assert.That(scenario.Attachments.Single().AttemptNumber).IsEqualTo(2);
    }

    [Test]
    public async Task Abandoned_factory_tasks_cannot_add_files_to_a_completed_row()
    {
        using var run = BDTestRun.Start();
        var fixture = new Capture(run, "test");
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<Exception?>? late = null;
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
        await Assert.That(await Required(late).WaitAsync(TimeSpan.FromSeconds(10))).IsTypeOf<InvalidOperationException>();
        await Assert.That(Required(outline.Examples).Single().Attachments).IsEmpty();
        await Assert.That(Directory.Exists(run.AttachmentFiles.DirectoryPath)).IsFalse();
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
        await Assert.That(() => fixture.Attach("nonempty")).ThrowsExactly<IOException>();
        var scenario = await fixture.When(() => Done()).Then(() => Done()).BDTestAsync();
        await Assert.That(Required(scenario.Attachments).Single().Length).IsZero();
        using var stream = run.OpenAttachment(Required(scenario.Attachments.Single().Id));
        await Assert.That(stream.ReadByte()).IsEqualTo(-1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
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

        await Assert.That(File.Exists(path)).IsEqualTo(createScenario);
        if (createScenario)
        {
            using var content = run.OpenAttachment(attachmentId);
            await Assert.That(new StreamReader(content).ReadToEnd()).IsEqualTo("abc");
            await Assert.That(() => new Capture(run, "next").Attach("abc")).ThrowsExactly<IOException>();
        }
        else
        {
            await Assert.That(() => run.OpenAttachment(attachmentId)).ThrowsExactly<KeyNotFoundException>();
            var next = new Capture(run, "next");
            next.Attach("abc");
            var scenario = await next.When(() => Done()).Then(() => Done()).BDTestAsync();
            await Assert.That(Required(scenario.Attachments).Single().Name).IsEqualTo("abc");
        }
    }

    [Test]
    // Keep the bounded disk-write probe independent of other file-store fixtures.
    [NotInParallel]
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

        await Assert.That(outline.Attachments).Count().IsEqualTo(1000);
        await Assert.That(Required(outline.Examples).All(row => Required(row.Attachments).Count == 1)).IsTrue();
        var files = Required(outline.Attachments).Concat(outline.Examples.SelectMany(row => Required(row.Attachments))).ToArray();

        await Assert.That((files).Length).IsEqualTo(1100);
        await Assert.That(files.Select(file => file.Name).Distinct().Count()).IsEqualTo(1100);
        foreach (var row in outline.Examples)
            await Assert.That(Required(row.Attachments).Any(file => file.Name == "factory-" + Required(row.ExampleValues)["Value"])).IsTrue();
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
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

        await Assert.That(await capture.WaitAsync(TimeSpan.FromSeconds(10))).IsTypeOf<InvalidOperationException>();
        await Assert.That(Directory.GetFiles(run.AttachmentFiles.DirectoryPath)).IsEmpty();
        await Assert.That(run.Scenarios.SelectMany(scenario => Required(scenario.Attachments))).IsEmpty();
        var next = new Capture(run, "next");
        next.Attach("abc");
        var nextScenario = await next.When(() => Done()).Then(() => Done()).BDTestAsync();
        await Assert.That(Required(nextScenario.Attachments).Single().Length).IsEqualTo(3);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
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
        await Assert.That(await capture.WaitAsync(TimeSpan.FromSeconds(10))).IsTypeOf<InvalidOperationException>();
        await Assert.That(Directory.Exists(run.AttachmentFiles.DirectoryPath)).IsFalse();
    }

    [Test]
    public async Task Setup_copy_finishing_during_an_outline_row_belongs_to_the_outline()
    {
        using var run = BDTestRun.Start();
        var fixture = new Capture(run, "outline");
        fixture.Start();
        using var source = new PausedStream();
        var capture = Task.Run(() => fixture.Attachments.Attach("setup", source, "text/plain"));
        await source.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Scenario outline;
        try
        {
            outline = await fixture.ScenarioOutline("row <Value>", new[] { new { Value = 1 }, new { Value = 2 } }, row =>
                fixture.When(async () =>
                {
                    source.Resume.Set();
                    await capture.WaitAsync(TimeSpan.FromSeconds(10));
                    fixture.Attach("step-" + row.Value);
                }).Then(() => Done())).BDTestAsync();
        }
        finally { source.Resume.Set(); }

        await Assert.That(Required(outline.Attachments).Single().Name).IsEqualTo("setup");
        await Assert.That(outline.Attachments.Single().AttemptNumber).IsEqualTo(1);
        await Assert.That(Required(outline.Examples).SelectMany(row => Required(row.Attachments))).IsEmpty();
        await Assert.That(outline.Examples.Select(row => Required(Required(row.Steps)[0].Attachments).Single().Name)).IsEquivalentTo((new[] { "step-1", "step-2" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Writer_created_before_begin_rejects_copy_finishing_after_teardown(bool useContext)
    {
        using var run = BDTestRun.Start();
        var fixture = new Capture(run, "constructor");
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
            fixture.Start();
            fixture.Finish();
        }
        finally { source.Resume.Set(); }

        await Assert.That(await capture.WaitAsync(TimeSpan.FromSeconds(10))).IsTypeOf<InvalidOperationException>();
        await Assert.That(Directory.GetFiles(run.AttachmentFiles.DirectoryPath)).IsEmpty();
        fixture.Start();
        await Assert.That(() => writer.Attach("old", new byte[] { 1 }, "text/plain")).ThrowsExactly<InvalidOperationException>();
        fixture.Attach("new");
        var scenario = await fixture.When(() => Done()).Then(() => Done()).BDTestAsync();
        await Assert.That(Required(scenario.Attachments).Single().Name).IsEqualTo("new");
        fixture.Finish();
    }
    [Test]
    public async Task Constructor_background_flow_cannot_get_a_new_writer_after_teardown()
    {
        using var run = BDTestRun.Start();
        var fixture = new Capture(run, "constructor");
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var capture = Task.Run(async () =>
        {
            await finish.Task;
            try { fixture.Attach("late"); return null; }
            catch (Exception exception) { return exception; }
        });
        fixture.Start();
        fixture.Finish();
        finish.SetResult();
        await Assert.That(await capture.WaitAsync(TimeSpan.FromSeconds(10))).IsTypeOf<InvalidOperationException>();
        await Assert.That(Directory.Exists(run.AttachmentFiles.DirectoryPath)).IsFalse();
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
