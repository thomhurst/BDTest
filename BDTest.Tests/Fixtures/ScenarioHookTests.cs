using BDTest.Interfaces;
using BDTest.Settings;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class ScenarioHookTests
{
    [Test]
    public async Task Hooks_wrap_steps_in_registration_order_and_receive_typed_context()
    {
        var calls = new List<string>();
        var options = new BDTestOptions();
        options.ScenarioHooks.Add(new Hook(s => Record("first before", s), s => Record("first after", s)));
        options.ScenarioHooks.Add(new Hook(s => Record("second before", s), s => Record("second after", s)));
        using var run = BDTestRun.Start(options);
        var test = new Fixture(run)
        {
            Before = s => Record("instance before", s),
            After = s => Record("instance after", s)
        };
        var scenario = await test.When(() => calls.Add("when")).Then(() => calls.Add("then")).BDTestAsync();
        Assert.That(calls, Is.EqualTo(new[] { "first before", "second before", "instance before", "when", "then",
            "instance after", "first after", "second after" }));
        Assert.That(scenario.Status, Is.EqualTo(Status.Passed));

        Task Record(string name, ScenarioHookContext s)
        {
            calls.Add(name);
            Assert.That(s.Context, Is.TypeOf<State>());
            Assert.That(s.Attempt, Is.Zero);
            Assert.That(s.CancellationToken.CanBeCanceled, Is.True);
            Assert.That(s.Scenario.Status, Is.EqualTo(name.EndsWith("before") ? Status.Pending : Status.Passed));
            return Task.CompletedTask;
        }
    }

    [TestCase("before")]
    [TestCase("step")]
    [TestCase("after")]
    public async Task Every_after_hook_runs_and_first_failure_wins(string failureAt)
    {
        var failure = new InvalidOperationException("original");
        var cleanup = new ApplicationException("cleanup");
        var calls = new List<string>();
        var options = new BDTestOptions();
        options.ScenarioHooks.Add(new Hook(_ => Task.CompletedTask, s =>
        {
            calls.Add("global");
            Assert.That(s.Scenario.Status, Is.EqualTo(Status.Failed));
            Assert.That(s.Scenario.Exception.Message, Is.EqualTo("original"));
            throw cleanup;
        }));
        using var run = BDTestRun.Start(options);
        var test = new Fixture(run)
        {
            Before = _ => failureAt == "before" ? throw failure : Task.CompletedTask,
            After = s =>
            {
                calls.Add("instance");
                if (failureAt != "after")
                    Assert.That(s.Scenario.Exception.Message, Is.EqualTo("original"));
                throw failureAt == "after" ? failure : cleanup;
            }
        };
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () => await test
            .When(() => RunStep()).Then(() => calls.Add("then")).BDTestAsync());
        Assert.That(thrown, Is.SameAs(failure));
        Assert.That(calls, Does.Contain("instance").And.Contain("global"));
        if (failureAt != "after") Assert.That(calls, Does.Not.Contain("then"));
        Assert.That(run.Scenarios.Single().Attempts.Single().Exception.Message, Is.EqualTo("original"));
        if (failureAt == "before") Assert.That(run.Scenarios.Single().Steps.All(s => s.Status == Status.NotRun), Is.True);

        void RunStep()
        {
            if (failureAt == "step") throw failure;
        }
    }

    [Test]
    public async Task Retry_and_outline_rows_get_fresh_contexts_and_attempt_tokens()
    {
        var before = new List<ScenarioHookContext<State>>();
        var after = new List<(int Attempt, Status Status, bool Cancelled)>();
        var options = new BDTestOptions();
        options.RetryTestRules.Add(e => e is TimeoutException, 1);
        using var run = BDTestRun.Start(options);
        var test = new Fixture(run)
        {
            Before = s => { before.Add(s); s.Context.Attempt = s.Attempt; return Task.CompletedTask; },
            After = s => { after.Add((s.Attempt, s.Scenario.Status, s.CancellationToken.IsCancellationRequested)); return Task.CompletedTask; }
        };
        var outline = await test.ScenarioOutline("row <Value>", new[] { new { Value = 1 }, new { Value = 2 } },
            row => test.When(async token =>
            {
                if (test.Context.Attempt == 0) await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }).Then(() => Complete()).WithScenarioTimeout(TimeSpan.FromMilliseconds(100))).BDTestAsync();
        Assert.That(outline.Status, Is.EqualTo(Status.Passed));
        Assert.That(before.Select(s => s.Context).Distinct().Count(), Is.EqualTo(4));
        Assert.That(before.Select(s => s.CancellationToken).Distinct().Count(), Is.EqualTo(4));
        Assert.That(before.Select(s => s.Attempt), Is.EqualTo(new[] { 0, 1, 0, 1 }));
        Assert.That(after, Is.EqualTo(new[] { (0, Status.Failed, true), (1, Status.Passed, false),
            (0, Status.Failed, true), (1, Status.Passed, false) }));
        Assert.That(before.All(s => s.Scenario.ExampleValues.ContainsKey("Value")), Is.True);
        Assert.That(outline.Examples.All(s => s.Exception == null), Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Timeout_callbacks_do_not_block_cleanup_and_token_is_cancelled_before_cleanup(bool callbackThrows)
    {
        using var run = BDTestRun.Start();
        using var releaseCallback = new ManualResetEventSlim();
        var callbackFinished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanupRan = false;
        var cancelledInCleanup = false;
        var test = new Fixture(run)
        {
            Before = s =>
            {
                s.CancellationToken.Register(() =>
                {
                    try
                    {
                        if (callbackThrows) throw new ApplicationException("callback failure");
                        releaseCallback.Wait();
                    }
                    finally { callbackFinished.TrySetResult(true); }
                });
                return Task.CompletedTask;
            },
            After = s =>
            {
                cleanupRan = true;
                cancelledInCleanup = s.CancellationToken.IsCancellationRequested
                    && s.CancellationToken.WaitHandle.WaitOne(0);
                releaseCallback.Set();
                return Task.CompletedTask;
            }
        };
        var execution = Task.Run(async () =>
        {
            try
            {
                await test.When(async token => await Task.Delay(Timeout.InfiniteTimeSpan, token))
                    .Then(() => Complete()).WithScenarioTimeout(TimeSpan.FromMilliseconds(100)).BDTestAsync();
                return (Exception)null;
            }
            catch (Exception exception) { return exception; }
        });
        try
        {
            var completed = await Task.WhenAny(execution, Task.Delay(TimeSpan.FromSeconds(10)));
            Assert.That(completed, Is.SameAs(execution), "Cancellation callbacks must not block cleanup.");
            Assert.That(await execution, Is.TypeOf<TimeoutException>());
            Assert.That(cleanupRan, Is.True);
            Assert.That(cancelledInCleanup, Is.True);
        }
        finally
        {
            releaseCallback.Set();
            await execution;
            await callbackFinished.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    [TestCase("passed")]
    [TestCase("skipped")]
    [TestCase("before failure")]
    [TestCase("step failure")]
    [TestCase("after failure")]
    public async Task Reports_preserve_hook_output_when_steps_run_or_are_bypassed(string outcome)
    {
        var options = new BDTestOptions();
        if (outcome == "skipped") options.SkipTestRules.Add(_ => true);
        options.ScenarioHooks.Add(new Hook(_ => Log("run before"), _ => Log("run after")));
        using var run = BDTestRun.Start(options);
        var fixture = new Fixture(run)
        {
            Before = async _ =>
            {
                await Log("instance before");
                if (outcome == "before failure") throw new InvalidOperationException("before failure");
            },
            After = async _ =>
            {
                await Log("instance after");
                if (outcome == "after failure") throw new InvalidOperationException("cleanup failure");
            }
        };
        try
        {
            await fixture.When(() =>
            {
                Console.WriteLine("step output");
                if (outcome == "step failure") throw new InvalidOperationException("step failure");
            }).Then(() => Complete()).BDTestAsync();
        }
        catch (InvalidOperationException) when (outcome.EndsWith("failure")) { }
        var output = run.Scenarios.Single().Output;
        foreach (var text in new[] { "run before", "instance before", "instance after", "run after" })
            Assert.That(output.Split(text).Length - 1, Is.EqualTo(1), $"Expected one occurrence of {text}.");
        if (outcome is "passed" or "step failure" or "after failure")
            Assert.That(output, Does.Contain("step output"));
        if (outcome == "after failure")
            Assert.That(output, Does.Contain("After-scenario hook failed:").And.Contain("cleanup failure"));

        static Task Log(string text) => Console.Out.WriteLineAsync(text);
    }

    [Test]
    public async Task Explicit_runs_freeze_and_isolate_registrations_from_global_events()
    {
        var firstCalls = 0;
        var secondCalls = 0;
        var globalCalls = 0;
        var options = new BDTestOptions();
        options.ScenarioHooks.Add(new Hook(_ => { Interlocked.Increment(ref firstCalls); return Task.CompletedTask; }));
        using var first = BDTestRun.Start(options);
        options.ScenarioHooks.Clear();
        options.ScenarioHooks.Add(new Hook(_ => { Interlocked.Increment(ref secondCalls); return Task.CompletedTask; }));
        using var second = BDTestRun.Start(options);
        options.ScenarioHooks.Clear();
        Task Global(ScenarioHookContext _) { Interlocked.Increment(ref globalCalls); return Task.CompletedTask; }
        BDTestSettings.BeforeScenario += Global;
        try
        {
            await Task.WhenAll(Task.Run(() => new Fixture(first).When(() => Complete()).Then(() => Complete()).BDTestAsync()),
                Task.Run(() => new Fixture(second).When(() => Complete()).Then(() => Complete()).BDTestAsync()));
            Assert.That(firstCalls, Is.EqualTo(1));
            Assert.That(secondCalls, Is.EqualTo(1));
            Assert.That(globalCalls, Is.Zero);
            await new Fixture(BDTestRun.Default).When(() => Complete()).Then(() => Complete()).BDTestAsync();
            Assert.That(globalCalls, Is.EqualTo(1));
        }
        finally { BDTestSettings.BeforeScenario -= Global; }
    }

    [Test]
    public async Task Rule_skips_still_run_scenario_hooks_without_running_steps()
    {
        var calls = new List<Status>();
        var options = new BDTestOptions();
        options.SkipTestRules.Add(_ => true);
        options.ScenarioHooks.Add(new Hook(s => { calls.Add(s.Scenario.Status); return Task.CompletedTask; },
            s => { calls.Add(s.Scenario.Status); return Task.CompletedTask; }));
        using var run = BDTestRun.Start(options);
        var scenario = await new Fixture(run).When(() => throw new Exception("must not run"))
            .Then(() => Complete()).BDTestAsync();
        Assert.That(calls, Is.EqualTo(new[] { Status.Pending, Status.Skipped }));
        Assert.That(scenario.Steps.All(s => s.Status == Status.Skipped), Is.True);
    }

    [Test]
    public async Task Scenario_hook_time_is_included_in_attempt_time_but_excluded_from_step_budget()
    {
        using var run = BDTestRun.Start();
        var test = new Fixture(run)
        {
            Before = async _ => await Task.Delay(150),
            After = async _ => await Task.Delay(150)
        };
        var scenario = await test.When(() => Complete()).Then(() => Complete())
            .WithScenarioTimeout(TimeSpan.FromMilliseconds(100)).BDTestAsync();
        Assert.That(scenario.TimeTaken, Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(300)));
        Assert.That(scenario.Attempts.Single().TimeTaken, Is.EqualTo(scenario.TimeTaken));
        Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
    }

    [TestCase("before")]
    [TestCase("after")]
    public async Task NUnit_hook_assertions_are_isolated_for_retries(string failureAt)
    {
        var options = new BDTestOptions();
        options.RetryTestRules.Add(e => e is AssertionException, 1);
        using var run = BDTestRun.Start(options);
        var after = new List<Status>();
        var fixture = new NUnitFixture(run)
        {
            Before = s =>
            {
                if (failureAt == "before") Assert.That(s.Attempt, Is.EqualTo(1));
                return Task.CompletedTask;
            },
            After = s =>
            {
                after.Add(s.Scenario.Status);
                if (failureAt == "after") Assert.That(s.Attempt, Is.EqualTo(1));
                return Task.CompletedTask;
            }
        };
        var scenario = await fixture.When(() => Complete()).Then(() => Complete()).BDTestAsync();
        Assert.That(scenario.RetryCount, Is.EqualTo(1));
        Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
        Assert.That(after, Is.EqualTo(new[] { failureAt == "before" ? Status.Failed : Status.Passed, Status.Passed }));
    }

    [Test]
    public async Task NUnit_caught_assertion_is_visible_to_after_scenario_hook()
    {
        using var run = BDTestRun.Start();
        Status? observed = null;
        var fixture = new NUnitFixture(run)
        {
            After = s => { observed = s.Scenario.Status; return Task.CompletedTask; }
        };
        await Assert.ThrowsAsync<AssertionException>(async () => await fixture.When(() => CaughtAssertion())
            .Then(() => Complete()).BDTestAsync());
        Assert.That(observed, Is.EqualTo(Status.Failed));

        static void CaughtAssertion()
        {
            try { Assert.Fail("caught failure"); }
            catch (AssertionException) { }
        }
    }

    [Test]
    public async Task NUnit_cleanup_diagnostics_do_not_replace_original_failure()
    {
        using var run = BDTestRun.Start();
        var failure = new InvalidOperationException("original step failure");
        var fixture = new NUnitFixture(run)
        {
            After = _ =>
            {
                for (var i = 0; i < 2; i++)
                {
                    try { Assert.Fail("secondary cleanup assertion"); }
                    catch (AssertionException) { }
                }
                throw new ApplicationException("secondary cleanup exception");
            }
        };
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () => await fixture
            .When(() => throw failure).Then(() => Complete()).BDTestAsync());
        Assert.That(thrown, Is.SameAs(failure));
    }

    [Test]
    public async Task Global_before_failure_stops_before_hooks_but_runs_all_after_hooks()
    {
        var calls = new List<string>();
        var options = new BDTestOptions();
        options.ScenarioHooks.Add(new Hook(_ => throw new InvalidOperationException("before"),
            _ => { calls.Add("first after"); return Task.CompletedTask; }));
        options.ScenarioHooks.Add(new Hook(_ => { calls.Add("second before"); return Task.CompletedTask; },
            _ => { calls.Add("second after"); return Task.CompletedTask; }));
        using var run = BDTestRun.Start(options);
        var fixture = new Fixture(run)
        {
            Before = _ => { calls.Add("instance before"); return Task.CompletedTask; },
            After = _ => { calls.Add("instance after"); return Task.CompletedTask; }
        };
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await fixture.When(() => Complete())
            .Then(() => Complete()).BDTestAsync());
        Assert.That(calls, Is.EqualTo(new[] { "instance after", "first after", "second after" }));
        Assert.That(run.Scenarios.Single().Steps.All(s => s.Status == Status.NotRun), Is.True);
    }

    [TestCase(Status.Skipped)]
    [TestCase(Status.Inconclusive)]
    [TestCase(Status.Passed)]
    public async Task Before_hook_exceptions_use_run_outcome_mappings(Status expected)
    {
        var options = new BDTestOptions();
        var types = expected == Status.Skipped ? options.SkipExceptionTypes
            : expected == Status.Passed ? options.SuccessExceptionTypes : options.InconclusiveExceptionTypes;
        types.Add(typeof(InvalidOperationException));
        using var run = BDTestRun.Start(options);
        Status? observed = null;
        var fixture = new Fixture(run)
        {
            Before = _ => throw new InvalidOperationException("classified"),
            After = s => { observed = s.Scenario.Status; return Task.CompletedTask; }
        };
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await fixture.When(() => Complete())
            .Then(() => Complete()).BDTestAsync());
        Assert.That(observed, Is.EqualTo(expected));
        Assert.That(run.Scenarios.Single().Status, Is.EqualTo(expected));
    }

    [Test]
    public void Null_scenario_hook_registration_is_rejected() => Assert.Throws<ArgumentException>(() =>
    {
        var options = new BDTestOptions();
        options.ScenarioHooks.Add(null);
        using var run = BDTestRun.Start(options);
    });

    private sealed class State { public int Attempt { get; set; } }
    private sealed class Fixture(BDTestRun run) : AbstractContextBDTestBase<State>
    {
        private readonly string _id = Guid.NewGuid().ToString("N");
        protected override string BDTestExecutionId => _id;
        public override BDTestRun TestRun => run;
        public Func<ScenarioHookContext<State>, Task> Before { get; init; } = _ => Task.CompletedTask;
        public Func<ScenarioHookContext<State>, Task> After { get; init; } = _ => Task.CompletedTask;
        public override Task OnBeforeScenario(ScenarioHookContext<State> scenario) => Before(scenario);
        public override Task OnAfterScenario(ScenarioHookContext<State> scenario) => After(scenario);
    }
    private sealed class NUnitFixture(BDTestRun run) : BDTest.NUnit.NUnitBDTestBase<State>
    {
        public override BDTestRun TestRun => run;
        public Func<ScenarioHookContext<State>, Task> Before { get; init; } = _ => Task.CompletedTask;
        public Func<ScenarioHookContext<State>, Task> After { get; init; } = _ => Task.CompletedTask;
        public override Task OnBeforeScenario(ScenarioHookContext<State> scenario) => Before(scenario);
        public override Task OnAfterScenario(ScenarioHookContext<State> scenario) => After(scenario);
    }
    private sealed class Hook(Func<ScenarioHookContext, Task> before, Func<ScenarioHookContext, Task> after = null) : IScenarioHook
    {
        public Task BeforeScenarioAsync(ScenarioHookContext scenario) => before(scenario);
        public Task AfterScenarioAsync(ScenarioHookContext scenario) => after?.Invoke(scenario) ?? Task.CompletedTask;
    }
    private static void Complete() { }
}
