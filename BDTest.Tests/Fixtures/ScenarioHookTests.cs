using Microsoft.Extensions.Time.Testing;
using BDTest.Interfaces;
using BDTest.Settings;
using BDTest.Test;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

public class ScenarioHookTests
{
    [Test]
    [Arguments(false, "before")]
    [Arguments(true, "before")]
    [Arguments(false, "step")]
    [Arguments(true, "step")]
    [Arguments(false, "after")]
    [Arguments(true, "after")]
    public async Task Hooks_keep_the_original_context_when_it_is_replaced_or_removed(bool remove, string boundary)
    {
        var contexts = new List<ScenarioHookContext>();
        var options = new BDTestOptions();
        Fixture? fixture = null;
        options.ScenarioHooks.Add(new Hook(s =>
        {
            contexts.Add(s);
            if (boundary == "before") ChangeContext();
            return Task.CompletedTask;
        }, s => { contexts.Add(s); return Task.CompletedTask; }));
        using var run = BDTestRun.Start(options);
        fixture = new Fixture(run)
        {
            Before = s => { contexts.Add(s); return Task.CompletedTask; },
            After = s =>
            {
                contexts.Add(s);
                if (boundary == "after") ChangeContext();
                return Task.CompletedTask;
            }
        };
        await fixture.When(() => ChangeDuringStep()).Then(() => Complete());
        await Assert.That(contexts).Count().IsEqualTo(4);
        foreach (var context in contexts) await Assert.That(context.Context).IsSameReferenceAs(contexts[0].Context);
        foreach (var context in contexts.Take(2)) await Assert.That(context.Scenario.Status).IsEqualTo(Status.Pending);
        foreach (var context in contexts.Skip(2)) await Assert.That(context.Scenario.Status).IsEqualTo(Status.Passed);
        await Assert.That(fixture.CreatedContexts).IsEqualTo(1).Because("Reading a hook must not create a replacement context.");

        void ChangeDuringStep() { if (boundary == "step") ChangeContext(); }
        void ChangeContext()
        {
            if (remove) fixture!.RemoveForTest();
            else fixture!.OverwriteContext(new State());
        }
    }

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
        await Assert.That(calls).IsEquivalentTo(new[] { "first before", "second before", "instance before", "when", "then",
            "instance after", "first after", "second after" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(scenario.Status).IsEqualTo(Status.Passed);

        async Task Record(string name, ScenarioHookContext s)
        {
            calls.Add(name);
            await Assert.That(s.Context).IsTypeOf<State>();
            await Assert.That(s.Attempt).IsZero();
            await Assert.That(s.CancellationToken.CanBeCanceled).IsTrue();
            await Assert.That(s.Scenario.Status).IsEqualTo(name.EndsWith("before") ? Status.Pending : Status.Passed);
            return;
        }
    }

    [Test]
    [Arguments("before")]
    [Arguments("step")]
    [Arguments("after")]
    public async Task Every_after_hook_runs_and_first_failure_wins(string failureAt)
    {
        var failure = new InvalidOperationException("original");
        var cleanup = new ApplicationException("cleanup");
        var calls = new List<string>();
        var options = new BDTestOptions();
        options.ScenarioHooks.Add(new Hook(_ => Task.CompletedTask, async s =>
        {
            calls.Add("global");
            await Assert.That(s.Scenario.Status).IsEqualTo(Status.Failed);
            await Assert.That(Required(s.Scenario.Exception).Message).IsEqualTo("original");
            throw cleanup;
        }));
        using var run = BDTestRun.Start(options);
        var test = new Fixture(run)
        {
            Before = _ => failureAt == "before" ? throw failure : Task.CompletedTask,
            After = async s =>
            {
                calls.Add("instance");
                if (failureAt != "after")
                    await Assert.That(Required(s.Scenario.Exception).Message).IsEqualTo("original");
                throw failureAt == "after" ? failure : cleanup;
            }
        };
        var thrown = await Assert.That(async () => await test
            .When(() => RunStep()).Then(() => calls.Add("then")).BDTestAsync()).ThrowsExactly<InvalidOperationException>();
        await Assert.That(thrown).IsSameReferenceAs(failure);

        await Assert.That(calls).Contains("instance").And.Contains("global");
        if (failureAt != "after") await Assert.That(calls).DoesNotContain("then");
        await Assert.That(Required(Required(run.Scenarios.Single().Attempts).Single().Exception).Message).IsEqualTo("original");
        if (failureAt == "before") await Assert.That(Required(run.Scenarios.Single().Steps).All(s => s.Status == Status.NotRun)).IsTrue();

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
        var clock = new FakeTimeProvider();
        var timeout = TimeSpan.FromMilliseconds(100);
        var options = new BDTestOptions { TimeProvider = clock };
        options.RetryTestRules.Add(e => e is TimeoutException, 1);
        using var run = BDTestRun.Start(options);
        var test = new Fixture(run)
        {
            Before = s => { before.Add(s); s.Context.Attempt = s.Attempt; return Task.CompletedTask; },
            After = s => { after.Add((s.Attempt, s.Scenario.Status, s.CancellationToken.IsCancellationRequested)); return Task.CompletedTask; }
        };
        var outline = await test.ScenarioOutline("row <Value>", new[] { new { Value = 1 }, new { Value = 2 } },
            row => test.When(() =>
            {
                if (test.Context.Attempt == 0)
                {
                    // Consume the budget in a completed step. Retry/context coverage must not
                    // depend on scheduling an async cancellation continuation within the grace period.
                    clock.Advance(timeout);
                }
            }).Then(() => Complete()).WithScenarioTimeout(timeout)).BDTestAsync()
            .WaitAsync(TimeSpan.FromSeconds(30)); // Deadlock guard only; scenario time is entirely virtual.
        await Assert.That(outline.Status).IsEqualTo(Status.Passed);
        await Assert.That(before.Select(s => s.Context).Distinct().Count()).IsEqualTo(4);
        await Assert.That(before.Select(s => s.CancellationToken).Distinct().Count()).IsEqualTo(4);
        await Assert.That(before.Select(s => s.Attempt)).IsEquivalentTo(new[] { 0, 1, 0, 1 }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(after).IsEquivalentTo(new[] { (0, Status.Failed, true), (1, Status.Passed, false),
            (0, Status.Failed, true), (1, Status.Passed, false) }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(before.All(s => Required(s.Scenario.ExampleValues).ContainsKey("Value"))).IsTrue();
        await Assert.That(Required(outline.Examples).All(s => s.Exception == null)).IsTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
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
                return (Exception?)null;
            }
            catch (Exception exception) { return exception; }
        });
        try
        {
            var completed = await Task.WhenAny(execution, Task.Delay(TimeSpan.FromSeconds(10)));
            await Assert.That(completed).IsSameReferenceAs(execution).Because("Cancellation callbacks must not block cleanup.");
            await Assert.That(await execution).IsTypeOf<TimeoutException>();
            await Assert.That(cleanupRan).IsTrue();
            await Assert.That(cancelledInCleanup).IsTrue();
        }
        finally
        {
            releaseCallback.Set();
            await execution;
            await callbackFinished.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    [Test]
    [Arguments("passed")]
    [Arguments("skipped")]
    [Arguments("before failure")]
    [Arguments("step failure")]
    [Arguments("after failure")]
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
        var output = Required(run.Scenarios.Single().Output);
        foreach (var text in new[] { "run before", "instance before", "instance after", "run after" })
            await Assert.That(output.Split(text).Length - 1).IsEqualTo(1).Because($"Expected one occurrence of {text}.");
        if (outcome is "passed" or "step failure" or "after failure")
            await Assert.That(output).Contains("step output");
        if (outcome == "after failure")

            await Assert.That(output).Contains("After-scenario hook failed:").And.Contains("cleanup failure");

        static Task Log(string text) => Console.Out.WriteLineAsync(text);
    }

    [Test]
    public async Task Explicit_runs_freeze_and_isolate_registrations_from_global_events()
    {
        var firstCalls = 0;
        var secondCalls = 0;
        var thirdCalls = 0;
        var options = new BDTestOptions();
        options.ScenarioHooks.Add(new Hook(_ => { Interlocked.Increment(ref firstCalls); return Task.CompletedTask; }));
        using var first = BDTestRun.Start(options);
        options.ScenarioHooks.Clear();
        options.ScenarioHooks.Add(new Hook(_ => { Interlocked.Increment(ref secondCalls); return Task.CompletedTask; }));
        using var second = BDTestRun.Start(options);
        options.ScenarioHooks.Clear();
        Task Third(ScenarioHookContext _) { Interlocked.Increment(ref thirdCalls); return Task.CompletedTask; }
        options.ScenarioHooks.Add(new Hook(Third));
        using var third = BDTestRun.Start(options);
        await Task.WhenAll(Task.Run(() => new Fixture(first).When(() => Complete()).Then(() => Complete()).BDTestAsync()),
            Task.Run(() => new Fixture(second).When(() => Complete()).Then(() => Complete()).BDTestAsync()));
        await Assert.That(firstCalls).IsEqualTo(1);
        await Assert.That(secondCalls).IsEqualTo(1);
        await Assert.That(thirdCalls).IsZero();
        await new Fixture(third).When(() => Complete()).Then(() => Complete()).BDTestAsync();
        await Assert.That(thirdCalls).IsEqualTo(1);

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
        await Assert.That(calls).IsEquivalentTo(new[] { Status.Pending, Status.Skipped }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(Required(scenario.Steps).All(s => s.Status == Status.Skipped)).IsTrue();
    }

    [Test]
    public async Task Scenario_hook_time_consumes_the_attempt_budget()
    {
        var clock = new FakeTimeProvider();
        using var run = BDTestRun.Start(new BDTestOptions { TimeProvider = clock });
        var hookDuration = TimeSpan.FromMilliseconds(150);
        var stepDuration = TimeSpan.FromMilliseconds(10);
        var test = new Fixture(run)
        {
            Before = _ => AdvanceHookTime(),
            After = _ => AdvanceHookTime()
        };
        await Assert.That(async () => await test.When(() => clock.Advance(stepDuration)).Then(() => clock.Advance(stepDuration))
            .WithScenarioTimeout(TimeSpan.FromMilliseconds(100)).BDTestAsync()).Throws<TimeoutException>();
        var scenario = run.Scenarios.Single();
        await Assert.That(scenario.TimeTaken).IsEqualTo(hookDuration * 2);
        await Assert.That(Required(scenario.Attempts).Single().TimeTaken).IsEqualTo(scenario.TimeTaken);
        await Assert.That(scenario.TimedOut).IsTrue();
        await Assert.That(scenario.Steps.All(step => step.Status == Status.NotRun)).IsTrue();
        await Assert.That(scenario.Status).IsEqualTo(Status.Failed);

        Task AdvanceHookTime()
        {
            clock.Advance(hookDuration);
            return Task.CompletedTask;
        }
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
        await Assert.That(async () => await fixture.When(() => Complete())
            .Then(() => Complete()).BDTestAsync()).ThrowsExactly<InvalidOperationException>();
        await Assert.That(calls).IsEquivalentTo(new[] { "instance after", "first after", "second after" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(Required(run.Scenarios.Single().Steps).All(s => s.Status == Status.NotRun)).IsTrue();
    }

    [Test]
    [Arguments(Status.Skipped)]
    [Arguments(Status.Inconclusive)]
    [Arguments(Status.Passed)]
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
        await Assert.That(async () => await fixture.When(() => Complete())
            .Then(() => Complete()).BDTestAsync()).ThrowsExactly<InvalidOperationException>();
        await Assert.That(observed).IsEqualTo(expected);
        await Assert.That(run.Scenarios.Single().Status).IsEqualTo(expected);
    }

    [Test]
    public async Task Null_scenario_hook_registration_is_rejected() => await Assert.That(() =>
    {
        var options = new BDTestOptions();
        options.ScenarioHooks.Add(null!);
        using var run = BDTestRun.Start(options);
    }).ThrowsExactly<ArgumentException>();

    private sealed class State { public int Attempt { get; set; } }
    private sealed class Fixture(BDTestRun run) : AbstractContextBDTestBase<State>
    {
        public int CreatedContexts { get; private set; }
        public void RemoveForTest() => RemoveContext();
        protected override async ValueTask<State> CreateContextAsync(ContextCreation creation)
        {
            CreatedContexts++;
            return await base.CreateContextAsync(creation);
        }
        private readonly string _id = Guid.NewGuid().ToString("N");
        protected override string BDTestExecutionId => _id;
        public override BDTestRun TestRun => run;
        public Func<ScenarioHookContext<State>, Task> Before { get; init; } = _ => Task.CompletedTask;
        public Func<ScenarioHookContext<State>, Task> After { get; init; } = _ => Task.CompletedTask;
        public override Task OnBeforeScenario(ScenarioHookContext<State> scenario) => Before(scenario);
        public override Task OnAfterScenario(ScenarioHookContext<State> scenario) => After(scenario);
    }
    private sealed class Hook(Func<ScenarioHookContext, Task> before, Func<ScenarioHookContext, Task>? after = null) : IScenarioHook
    {
        public Task BeforeScenarioAsync(ScenarioHookContext scenario) => before(scenario);
        public Task AfterScenarioAsync(ScenarioHookContext scenario) => after?.Invoke(scenario) ?? Task.CompletedTask;
    }
    private static void Complete() { }
}
