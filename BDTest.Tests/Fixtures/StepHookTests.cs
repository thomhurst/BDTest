using BDTest.Attributes;
using BDTest.Settings;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
public class StepHookTests : TestRunOwner
{
    [Test]
    public async Task Hooks_are_awaited_in_order_for_each_step()
    {
        var calls = new List<string>();
        Func<Step, Task> before = async step =>
        {
            await Task.Yield();
            calls.Add("run before");
        };
        Func<Step, Task> after = async step =>
        {
            await Task.Yield();
            Assert.That(step.Status, Is.EqualTo(Status.Passed));
            Assert.That(step.EndTime, Is.GreaterThanOrEqualTo(step.StartTime));
            calls.Add("run after");
        };
        Func<Step, Task> secondBefore = async _ => { await Task.Yield(); calls.Add("second before"); };
        Func<Step, Task> secondAfter = async _ => { await Task.Yield(); calls.Add("second after"); };
        Options.StepHooks.Add(new CallbackStepHook(before: before));
        Options.StepHooks.Add(new CallbackStepHook(before: secondBefore));
        Options.StepHooks.Add(new CallbackStepHook(after: after));
        Options.StepHooks.Add(new CallbackStepHook(after: secondAfter));
        using var test = new HookTest(() => TestRun)
        {
            Before = async step => { await Task.Yield(); calls.Add("test before"); },
            After = async step => { await Task.Yield(); calls.Add("test after"); }
        };
        var scenario = await test.When(() => calls.Add("when"))
            .Then(() => calls.Add("then")).BDTestAsync();

        Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
        Assert.That(calls, Is.EqualTo(new[]
        {
            "run before", "second before", "test before", "when", "test after", "run after", "second after",
            "run before", "second before", "test before", "then", "test after", "run after", "second after"
        }));

    }

    [Test]
    public async Task Failure_keeps_original_exception_and_runs_all_after_hooks()
    {
        var original = new InvalidOperationException("step failed");
        var calls = new List<string>();
        Step? observed = null;
        Status? observedStatus = null;
        string? observedException = null;
        Func<Step, Task> after = async _ =>
        {
            await Task.Yield();
            calls.Add("run after failed");
            throw new Exception("run hook failed");
        };
        Func<Step, Task> lastAfter = _ => { calls.Add("last after"); return Task.CompletedTask; };
        Options.StepHooks.Add(new CallbackStepHook(after: after));
        Options.StepHooks.Add(new CallbackStepHook(after: lastAfter));
        using var test = new HookTest(() => TestRun)
        {
            After = step =>
            {
                observed = step;
                observedStatus = step.Status;
                observedException = step.Exception?.Message;
                calls.Add("test after failed");
                throw new Exception("test hook failed");
            }
        };
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await test.When(() => Throw(original)).Then(() => calls.Add("unreached")).BDTestAsync());

        Assert.That(thrown, Is.SameAs(original));
        Assert.That(observedStatus, Is.EqualTo(Status.Failed));
        Assert.That(observedException, Is.EqualTo(original.Message));
        Assert.That(Required(Required(observed).Exception).Message, Is.EqualTo(original.Message));
        Assert.That(calls, Is.EqualTo(new[] { "test after failed", "run after failed", "last after" }));
        Assert.That(GetScenario(Required(observed)).Status, Is.EqualTo(Status.Failed));
        Assert.That(Required(GetScenario(Required(observed)).Steps)[1].Status, Is.EqualTo(Status.NotRun));

    }

    [TestCase("before", "failure", Status.Failed)]
    [TestCase("step", "failure", Status.Failed)]
    [TestCase("after", "failure", Status.Failed)]
    [TestCase("before", "not implemented", Status.NotImplemented)]
    [TestCase("step", "not implemented", Status.NotImplemented)]
    [TestCase("after", "not implemented", Status.NotImplemented)]
    [TestCase("before", "success", Status.Passed)]
    [TestCase("step", "success", Status.Passed)]
    [TestCase("after", "success", Status.Passed)]
    [TestCase("before", "skip", Status.Skipped)]
    [TestCase("step", "skip", Status.Skipped)]
    [TestCase("after", "skip", Status.Skipped)]
    [TestCase("before", "inconclusive", Status.Inconclusive)]
    [TestCase("step", "inconclusive", Status.Inconclusive)]
    [TestCase("after", "inconclusive", Status.Inconclusive)]
    public async Task Hook_exceptions_use_existing_result_classification(string phase, string kind, Status expected)
    {
        Options.SuccessExceptionTypes.Add(typeof(HookSuccessException));
        Options.SkipExceptionTypes.Add(typeof(HookSkipException));
        Options.InconclusiveExceptionTypes.Add(typeof(HookInconclusiveException));
        Exception exception = kind switch
        {
            "not implemented" => new NotImplementedException("not implemented"),
            "success" => new HookSuccessException(),
            "skip" => new HookSkipException(),
            "inconclusive" => new HookInconclusiveException(),
            _ => new InvalidOperationException("failure")
        };
        var afterCalls = 0;
        var stepCalls = new List<string>();
        Step? observed = null;
        Status? observedStatus = null;
        using var test = new HookTest(() => TestRun)
        {
            Before = _ => phase == "before" ? Task.FromException(exception) : Task.CompletedTask,
            After = step =>
            {
                observed = step;
                observedStatus = step.Status;
                afterCalls++;
                return phase == "after" ? Task.FromException(exception) : Task.CompletedTask;
            }
        };

        var thrown = await Assert.ThrowsAsync(Required(exception).GetType(), async () =>
            await test.When(() => RunOrThrow(phase == "step", exception, stepCalls))
                .Then(() => stepCalls.Add("unreached")).BDTestAsync());

        Assert.That(thrown, Is.SameAs(exception));
        Assert.That(afterCalls, Is.EqualTo(1));
        Assert.That(observedStatus, Is.EqualTo(phase == "after" ? Status.Passed : expected));
        Assert.That(Required(observed).Status, Is.EqualTo(expected));
        Assert.That(GetScenario(Required(observed)).Status, Is.EqualTo(expected));
        Assert.That(stepCalls, Is.EqualTo(phase == "before" ? Array.Empty<string>() : new[] { "step" }));
        Assert.That(Required(observed).Exception?.Message,
            Is.EqualTo(expected is Status.Failed or Status.NotImplemented ? Required(exception).Message : null));
    }

    [Test]
    public async Task Run_before_failure_skips_body_but_still_runs_after_hooks()
    {
        var calls = new List<string>();
        Func<Step, Task> before = _ => throw new InvalidOperationException("before failed");
        Func<Step, Task> after = _ => { calls.Add("run after"); return Task.CompletedTask; };
        Options.StepHooks.Add(new CallbackStepHook(before: before));
        Options.StepHooks.Add(new CallbackStepHook(after: after));
        using var test = new HookTest(() => TestRun)
        {
            Before = _ => { calls.Add("unreached before"); return Task.CompletedTask; },
            After = _ => { calls.Add("test after"); return Task.CompletedTask; }
        };
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await test.When(() => calls.Add("unreached body")).Then(() => NoOp()).BDTestAsync());
        Assert.That(calls, Is.EqualTo(new[] { "test after", "run after" }));

    }

    [Test]
    public async Task Run_after_failure_fails_a_successful_step_and_other_handlers_still_run()
    {
        var exception = new InvalidOperationException("after failed");
        var calls = new List<string>();
        Step? observed = null;
        Func<Step, Task> after = _ => Task.FromException(exception);
        Func<Step, Task> last = step => { observed = step; calls.Add("last"); return Task.CompletedTask; };
        Options.StepHooks.Add(new CallbackStepHook(after: after));
        Options.StepHooks.Add(new CallbackStepHook(after: last));
        using var test = new HookTest(() => TestRun);
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await test.When(() => calls.Add("body")).Then(() => calls.Add("unreached")).BDTestAsync());
        Assert.That(thrown, Is.SameAs(exception));
        Assert.That(calls, Is.EqualTo(new[] { "body", "last" }));
        Assert.That(Required(observed).Status, Is.EqualTo(Status.Failed));
        Assert.That(Required(Required(observed).Exception).Message, Is.EqualTo(Required(exception).Message));

    }

    [Test]
    public async Task Individually_skipped_steps_run_hooks_but_skipped_scenarios_do_not()
    {
        Options.SkipStepRules.Add<HookSkipAttribute>(_ => true);
        var results = new List<Status>();
        using var test = new HookTest(() => TestRun) { After = step => { results.Add(step.Status); return Task.CompletedTask; } };
        await test.When(() => SkippedStep()).Then(() => NoOp()).BDTestAsync();
        Assert.That(results, Is.EqualTo(new[] { Status.Skipped, Status.Passed }));

        var options = new BDTestOptions();
        options.SkipTestRules.Add(_ => true);
        using var skippedRun = BDTestRun.Start(options);
        using var skippedTest = new HookTest(() => skippedRun) { After = step => { results.Add(step.Status); return Task.CompletedTask; } };
        results.Clear();
        var scenario = await skippedTest.When(() => NoOp()).Then(() => NoOp()).BDTestAsync();
        Assert.That(scenario.Status, Is.EqualTo(Status.Skipped));
        Assert.That(results, Is.Empty);
    }

    [Test]
    [BDTestRetry(1)]
    public async Task Hooks_run_for_each_retry_with_reset_results()
    {
        var beforeResults = new List<(Status Status, bool IsRetry)>();
        var afterResults = new List<(Status Status, bool IsRetry)>();
        var calls = new List<string>();
        using var test = new HookTest(() => TestRun)
        {
            Before = step => { beforeResults.Add((step.Status, step.IsRetry)); return Task.CompletedTask; },
            After = step => { afterResults.Add((step.Status, step.IsRetry)); return Task.CompletedTask; }
        };
        var scenario = await test.When(() => FailOnce(calls)).Then(() => NoOp()).BDTestAsync();
        Assert.That(scenario.RetryCount, Is.EqualTo(1));
        Assert.That(beforeResults, Is.EqualTo(new[]
        {
            (Status.Pending, false), (Status.Pending, true), (Status.Pending, true)
        }));
        Assert.That(afterResults, Is.EqualTo(new[]
        {
            (Status.Failed, false), (Status.Passed, true), (Status.Passed, true)
        }));
    }

    [Test]
    public async Task Registration_changes_do_not_mutate_the_running_snapshot()
    {
        var calls = new List<string>();
        Func<Step, Task> added = _ => { calls.Add("added"); return Task.CompletedTask; };
        var registered = false;
        Func<Step, Task> before = _ =>
        {
            if (!registered)
            {
                registered = true;
                Options.StepHooks.Add(new CallbackStepHook(after: added));
            }
            return Task.CompletedTask;
        };
        Options.StepHooks.Add(new CallbackStepHook(before: before));
        using var first = new HookTest(() => TestRun);
        await first.When(() => calls.Add("when")).Then(() => calls.Add("then")).BDTestAsync();
        Assert.That(calls, Is.EqualTo(new[] { "when", "then" }));

        calls.Clear();
        using var second = new HookTest(() => TestRun);
        await second.When(() => NoOp()).Then(() => NoOp()).BDTestAsync();
        Assert.That(calls, Is.Empty);
    }

    [Test]
    public async Task Per_test_hooks_remain_isolated_during_parallel_execution()
    {
        var entered = 0;
        var bothEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tests = Enumerable.Range(0, 2).Select(async id =>
        {
            var steps = new List<Step>();
            using var test = new HookTest(() => TestRun)
            {
                Before = async step =>
                {
                    if (Interlocked.Increment(ref entered) == 2)
                    {
                        bothEntered.TrySetResult();
                    }
                    await bothEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    steps.Add(step);
                }
            };
            var scenario = await test.When(() => NoOp()).Then(() => NoOp()).BDTestAsync();
            Assert.That(steps, Is.EqualTo(scenario.Steps));
            Assert.That(steps.All(step => step.ScenarioId == scenario.Guid), Is.True);
            return scenario;
        });
        var scenarios = await Task.WhenAll(tests);
        Assert.That(scenarios[0].FrameworkTestId, Is.Not.EqualTo(scenarios[1].FrameworkTestId));
    }

    [Test]
    public async Task After_hooks_receive_step_output_and_their_output_is_captured()
    {
        var outputs = new List<string>();
        using var test = new HookTest(() => TestRun)
        {
            Before = async _ => { await Console.Out.WriteLineAsync("before output"); },
            After = async step =>
            {
                outputs.Add(Required(step.Output));
                await Console.Out.WriteLineAsync("after output");
            }
        };
        var scenario = await test.When(() => Console.WriteLine("body output")).Then(() => NoOp()).BDTestAsync();
        Assert.That(outputs[0], Does.Contain("before output").And.Contain("body output").And.Not.Contain("after output"));
        Assert.That(outputs[1], Is.EqualTo("before output"));
        Assert.That(Required(scenario.Steps)[0].Output, Does.Contain("after output"));
        Assert.That(scenario.Output, Does.Contain("body output").And.Contain("after output"));
    }

    [Test]
    public async Task Timeout_remains_primary_when_after_hook_throws()
    {
        Step? observed = null;
        using var test = new HookTest(() => TestRun)
        {
            After = step =>
            {
                observed = step;
                Assert.That(step.TimedOut, Is.True);
                Assert.That(step.Status, Is.EqualTo(Status.Failed));
                throw new InvalidOperationException("after hook failed");
            }
        };
        var builder = test.When(token => Task.Delay(Timeout.Infinite, token))
            .WithStepTimeout(TimeSpan.FromMilliseconds(150)).Then(() => NoOp());

        var exception = await Assert.ThrowsAsync<TimeoutException>(async () =>
            await builder.BDTestAsync().WaitAsync(TimeSpan.FromSeconds(10)));

        Assert.That(Required(Required(observed).Exception).Message, Is.EqualTo(Required(exception).Message));
        Assert.That(GetScenario(Required(observed)).Status, Is.EqualTo(Status.Failed));
        Assert.That(Required(GetScenario(Required(observed)).Steps)[1].Status, Is.EqualTo(Status.NotRun));
    }

    [Test]
    public async Task Before_hook_time_reduces_remaining_scenario_budget()
    {
        var calls = new List<string>();
        Step? observed = null;
        using var test = new HookTest(() => TestRun)
        {
            Before = async _ => await Task.Delay(150),
            After = step => { observed = step; calls.Add("after"); return Task.CompletedTask; }
        };
        var builder = test.When(() => calls.Add("body")).Then(() => calls.Add("later"))
            .WithScenarioTimeout(TimeSpan.FromMilliseconds(50));

        var exception = await Assert.ThrowsAsync<TimeoutException>(async () =>
            await builder.BDTestAsync().WaitAsync(TimeSpan.FromSeconds(10)));

        Assert.That(Required(exception).Message, Does.StartWith("Scenario"));
        Assert.That(calls, Is.EqualTo(new[] { "after" }));
        Assert.That(Required(observed).TimedOut, Is.True);
        Assert.That(GetScenario(Required(observed)).Status, Is.EqualTo(Status.Failed));
    }

    [TestCase(false, "failure")]
    [TestCase(true, "failure")]
    [TestCase(false, "success")]
    [TestCase(true, "success")]
    [TestCase(false, "skip")]
    [TestCase(true, "skip")]
    [TestCase(false, "inconclusive")]
    [TestCase(true, "inconclusive")]
    [TestCase(false, "not implemented")]
    [TestCase(true, "not implemented")]
    public async Task Expired_before_hook_failure_is_a_scenario_timeout(bool runHook, string kind)
    {
        Options.SuccessExceptionTypes.Add(typeof(HookSuccessException));
        Options.SkipExceptionTypes.Add(typeof(HookSkipException));
        Options.InconclusiveExceptionTypes.Add(typeof(HookInconclusiveException));
        Exception failure = kind switch
        {
            "success" => new HookSuccessException(),
            "skip" => new HookSkipException(),
            "inconclusive" => new HookInconclusiveException(),
            "not implemented" => new NotImplementedException("before hook not implemented"),
            _ => new InvalidOperationException("before hook failed")
        };
        var calls = new List<string>();
        Step? observed = null;
        Func<Step, Task> before = async _ =>
        {
            await Task.Delay(150);
            throw failure;
        };
        using var test = new HookTest(() => TestRun)
        {
            Before = runHook ? _ => Task.CompletedTask : before,
            After = step => { observed = step; calls.Add("after"); return Task.CompletedTask; }
        };
        if (runHook) { Options.StepHooks.Add(new CallbackStepHook(before: before)); }
        var builder = test.When(() => calls.Add("body")).Then(() => calls.Add("later"))
            .WithScenarioTimeout(TimeSpan.FromMilliseconds(50));
        var exception = await Assert.ThrowsAsync<TimeoutException>(async () =>
            await builder.BDTestAsync().WaitAsync(TimeSpan.FromSeconds(10)));

        Assert.That(Required(exception).Message, Does.StartWith("Scenario"));
        Assert.That(calls, Is.EqualTo(new[] { "after" }));
        Assert.That(Required(observed).TimedOut, Is.True);
        Assert.That(Required(observed).Status, Is.EqualTo(Status.Failed));
        Assert.That(Required(Required(observed).Exception).Message, Is.EqualTo(Required(exception).Message));
        Assert.That(GetScenario(Required(observed)).TimedOut, Is.True);
        Assert.That(GetScenario(Required(observed)).Status, Is.EqualTo(Status.Failed));
        Assert.That(Required(GetScenario(Required(observed)).Steps)[1].Status, Is.EqualTo(Status.NotRun));

    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Before_hook_failure_within_budget_survives_after_hook_expiry(bool runHook)
    {
        var failure = new InvalidOperationException("before hook failed within budget");
        Step? observed = null;
        Func<Step, Task> before = _ => throw failure;
        using var test = new HookTest(() => TestRun)
        {
            Before = runHook ? _ => Task.CompletedTask : before,
            After = async step => { observed = step; await Task.Delay(1500); }
        };
        if (runHook) { Options.StepHooks.Add(new CallbackStepHook(before: before)); }
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await test.When(() => NoOp()).Then(() => NoOp())
                .WithScenarioTimeout(TimeSpan.FromSeconds(1)).BDTestAsync().WaitAsync(TimeSpan.FromSeconds(10)));

        Assert.That(thrown, Is.SameAs(failure));
        Assert.That(Required(observed).TimedOut, Is.False);
        Assert.That(Required(Required(observed).Exception).Message, Is.EqualTo(failure.Message));
        Assert.That(GetScenario(Required(observed)).TimedOut, Is.False);
        Assert.That(GetScenario(Required(observed)).Status, Is.EqualTo(Status.Failed));

    }

    [TestCase(false, "none")]
    [TestCase(true, "none")]
    [TestCase(false, "failure")]
    [TestCase(true, "failure")]
    [TestCase(true, "success")]
    [TestCase(true, "skip")]
    [TestCase(true, "inconclusive")]
    [TestCase(true, "not implemented")]
    public async Task After_hook_expiry_fails_the_active_step_even_when_it_is_last(bool finalStep, string kind)
    {
        Options.SuccessExceptionTypes.Add(typeof(HookSuccessException));
        Options.SkipExceptionTypes.Add(typeof(HookSkipException));
        Options.InconclusiveExceptionTypes.Add(typeof(HookInconclusiveException));
        var calls = new List<string>();
        Step? observed = null;
        using var test = new HookTest(() => TestRun)
        {
            After = async step =>
            {
                if (calls.Count == 0) { return; }
                observed = step;
                await Task.Delay(1500);
                calls.Add("after");
                switch (kind)
                {
                    case "failure": throw new InvalidOperationException("after hook failed");
                    case "success": throw new HookSuccessException();
                    case "skip": throw new HookSkipException();
                    case "inconclusive": throw new HookInconclusiveException();
                    case "not implemented": throw new NotImplementedException("after hook not implemented");
                }
            }
        };
        var first = test.When(() => NoOp()).Then(() => calls.Add("body"));
        RunnableStepBuilder builder = finalStep ? first : first.And(() => calls.Add("later"));
        _ = builder.WithScenarioTimeout(TimeSpan.FromSeconds(1));

        var exception = await Assert.ThrowsAsync<TimeoutException>(async () =>
            await builder.BDTestAsync().WaitAsync(TimeSpan.FromSeconds(10)));

        Assert.That(Required(exception).Message, Does.StartWith("Scenario"));
        Assert.That(calls, Is.EqualTo(new[] { "body", "after" }));
        Assert.That(Required(observed).TimedOut, Is.True);
        Assert.That(Required(observed).Status, Is.EqualTo(Status.Failed));
        Assert.That(Required(Required(observed).Exception).Message, Is.EqualTo(Required(exception).Message));
        Assert.That(GetScenario(Required(observed)).TimedOut, Is.True);
        Assert.That(GetScenario(Required(observed)).Status, Is.EqualTo(Status.Failed));
        if (!finalStep)
        {
            Assert.That(Required(GetScenario(Required(observed)).Steps)[2].Status, Is.EqualTo(Status.NotRun));
        }
    }

    private Scenario GetScenario(Step step) => BDTestUtil.GetScenarios(TestRun).Single(scenario => scenario.Guid == step.ScenarioId);
    private static void Throw(Exception exception) => throw exception;
    private static void NoOp() { }
    private static void RunOrThrow(bool fail, Exception exception, List<string> calls)
    {
        calls.Add("step");
        if (fail) { throw exception; }
    }
    private static void FailOnce(List<string> calls)
    {
        calls.Add("step");
        if (calls.Count == 1) { throw new InvalidOperationException("retry me"); }
    }
    [HookSkip]
    private static void SkippedStep() => throw new AssertionException("Skipped body must not run.");

    private sealed class HookSkipAttribute : SkipStepAttribute;
    private sealed class HookSuccessException : Exception;
    private sealed class HookSkipException : Exception;
    private sealed class HookInconclusiveException : Exception;

    private sealed class HookTest(Func<BDTestRun> run) : BDTestBase, IDisposable
    {
        private readonly string _id = Guid.NewGuid().ToString("N");
        protected override string BDTestExecutionId => _id;
        public string ExecutionId => _id;
        private bool _bound;
        public override BDTestRun TestRun
        {
            get
            {
                var owner = run();
                if (!_bound) { _bound = true; BeginTest(_id, Required(global::NUnit.Framework.Internal.TestExecutionContext.CurrentContext.CurrentTest.Method).MethodInfo); }
                return owner;
            }
        }
        public void Dispose() => EndTest();
        public Func<Step, Task> Before { get; init; } = _ => Task.CompletedTask;
        public Func<Step, Task> After { get; init; } = _ => Task.CompletedTask;
        public override Task OnBeforeStep(Step step) => Before(step);
        public override Task OnAfterStep(Step step) => After(step);
    }
}
