using BDTest.Attributes;
using BDTest.Settings;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
public class StepHookTests
{
    [Test]
    public async Task Hooks_are_awaited_in_order_for_each_step()
    {
        var calls = new List<string>();
        Func<Step, Task> before = async step =>
        {
            await Task.Yield();
            calls.Add("global before");
        };
        Func<Step, Task> after = async step =>
        {
            await Task.Yield();
            Assert.That(step.Status, Is.EqualTo(Status.Passed));
            Assert.That(step.EndTime, Is.GreaterThanOrEqualTo(step.StartTime));
            calls.Add("global after");
        };
        Func<Step, Task> secondBefore = async _ => { await Task.Yield(); calls.Add("second before"); };
        Func<Step, Task> secondAfter = async _ => { await Task.Yield(); calls.Add("second after"); };
        BDTestSettings.BeforeStep += before;
        BDTestSettings.BeforeStep += secondBefore;
        BDTestSettings.AfterStep += after;
        BDTestSettings.AfterStep += secondAfter;
        try
        {
            var test = new HookTest
            {
                Before = async step => { await Task.Yield(); calls.Add("test before"); },
                After = async step => { await Task.Yield(); calls.Add("test after"); }
            };
            var scenario = await test.When(() => calls.Add("when"))
                .Then(() => calls.Add("then")).BDTestAsync();

            Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
            Assert.That(calls, Is.EqualTo(new[]
            {
                "global before", "second before", "test before", "when", "test after", "global after", "second after",
                "global before", "second before", "test before", "then", "test after", "global after", "second after"
            }));
        }
        finally
        {
            BDTestSettings.BeforeStep -= before;
            BDTestSettings.BeforeStep -= secondBefore;
            BDTestSettings.AfterStep -= after;
            BDTestSettings.AfterStep -= secondAfter;
        }
    }

    [Test]
    public async Task Failure_keeps_original_exception_and_runs_all_after_hooks()
    {
        var original = new InvalidOperationException("step failed");
        var calls = new List<string>();
        Step observed = null;
        Status? observedStatus = null;
        string observedException = null;
        Func<Step, Task> after = async _ =>
        {
            await Task.Yield();
            calls.Add("global after failed");
            throw new Exception("global hook failed");
        };
        Func<Step, Task> lastAfter = _ => { calls.Add("last after"); return Task.CompletedTask; };
        BDTestSettings.AfterStep += after;
        BDTestSettings.AfterStep += lastAfter;
        try
        {
            var test = new HookTest
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
            Assert.That(observed.Exception.Message, Is.EqualTo(original.Message));
            Assert.That(calls, Is.EqualTo(new[] { "test after failed", "global after failed", "last after" }));
            Assert.That(GetScenario(observed).Status, Is.EqualTo(Status.Failed));
            Assert.That(GetScenario(observed).Steps[1].Status, Is.EqualTo(Status.Inconclusive));
        }
        finally
        {
            BDTestSettings.AfterStep -= after;
            BDTestSettings.AfterStep -= lastAfter;
        }
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
        BDTestSettings.CustomExceptionSettings.SuccessExceptionTypes.Add(typeof(HookSuccessException));
        BDTestSettings.CustomExceptionSettings.SkipExceptionTypes.Add(typeof(HookSkipException));
        BDTestSettings.CustomExceptionSettings.InconclusiveExceptionTypes.Add(typeof(HookInconclusiveException));
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
        Step observed = null;
        Status? observedStatus = null;
        var test = new HookTest
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

        var thrown = await Assert.ThrowsAsync(exception.GetType(), async () =>
            await test.When(() => RunOrThrow(phase == "step", exception, stepCalls))
                .Then(() => stepCalls.Add("unreached")).BDTestAsync());

        Assert.That(thrown, Is.SameAs(exception));
        Assert.That(afterCalls, Is.EqualTo(1));
        Assert.That(observedStatus, Is.EqualTo(phase == "after" ? Status.Passed : expected));
        Assert.That(observed.Status, Is.EqualTo(expected));
        Assert.That(GetScenario(observed).Status, Is.EqualTo(expected));
        Assert.That(stepCalls, Is.EqualTo(phase == "before" ? Array.Empty<string>() : new[] { "step" }));
        Assert.That(observed.Exception?.Message,
            Is.EqualTo(expected is Status.Failed or Status.NotImplemented ? exception.Message : null));
    }

    [Test]
    public async Task Global_before_failure_skips_body_but_still_runs_after_hooks()
    {
        var calls = new List<string>();
        Func<Step, Task> before = _ => throw new InvalidOperationException("before failed");
        Func<Step, Task> after = _ => { calls.Add("global after"); return Task.CompletedTask; };
        BDTestSettings.BeforeStep += before;
        BDTestSettings.AfterStep += after;
        try
        {
            var test = new HookTest
            {
                Before = _ => { calls.Add("unreached before"); return Task.CompletedTask; },
                After = _ => { calls.Add("test after"); return Task.CompletedTask; }
            };
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await test.When(() => calls.Add("unreached body")).Then(() => NoOp()).BDTestAsync());
            Assert.That(calls, Is.EqualTo(new[] { "test after", "global after" }));
        }
        finally
        {
            BDTestSettings.BeforeStep -= before;
            BDTestSettings.AfterStep -= after;
        }
    }

    [Test]
    public async Task Global_after_failure_fails_a_successful_step_and_other_handlers_still_run()
    {
        var exception = new InvalidOperationException("after failed");
        var calls = new List<string>();
        Step observed = null;
        Func<Step, Task> after = _ => Task.FromException(exception);
        Func<Step, Task> last = step => { observed = step; calls.Add("last"); return Task.CompletedTask; };
        BDTestSettings.AfterStep += after;
        BDTestSettings.AfterStep += last;
        try
        {
            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await new HookTest().When(() => calls.Add("body")).Then(() => calls.Add("unreached")).BDTestAsync());
            Assert.That(thrown, Is.SameAs(exception));
            Assert.That(calls, Is.EqualTo(new[] { "body", "last" }));
            Assert.That(observed.Status, Is.EqualTo(Status.Failed));
            Assert.That(observed.Exception.Message, Is.EqualTo(exception.Message));
        }
        finally
        {
            BDTestSettings.AfterStep -= after;
            BDTestSettings.AfterStep -= last;
        }
    }

    [Test]
    public async Task Individually_skipped_steps_run_hooks_but_skipped_scenarios_do_not()
    {
        BDTestSettings.GlobalSkipStepRules.Add<HookSkipAttribute>(_ => true);
        var results = new List<Status>();
        var test = new HookTest { After = step => { results.Add(step.Status); return Task.CompletedTask; } };
        await test.When(() => SkippedStep()).Then(() => NoOp()).BDTestAsync();
        Assert.That(results, Is.EqualTo(new[] { Status.Skipped, Status.Passed }));

        var executionId = test.ExecutionId;
        BDTestSettings.GlobalSkipTestRules.Add(scenario => scenario.FrameworkTestId == executionId);
        results.Clear();
        var scenario = await test.When(() => NoOp()).Then(() => NoOp()).BDTestAsync();
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
        var test = new HookTest
        {
            Before = step => { beforeResults.Add((step.Status, step.IsRetry)); return Task.CompletedTask; },
            After = step => { afterResults.Add((step.Status, step.IsRetry)); return Task.CompletedTask; }
        };
        var scenario = await test.When(() => FailOnce(calls)).Then(() => NoOp()).BDTestAsync();
        Assert.That(scenario.RetryCount, Is.EqualTo(1));
        Assert.That(beforeResults, Is.EqualTo(new[]
        {
            (Status.Inconclusive, false), (Status.Inconclusive, true), (Status.Inconclusive, true)
        }));
        Assert.That(afterResults, Is.EqualTo(new[]
        {
            (Status.Failed, false), (Status.Passed, true), (Status.Passed, true)
        }));
    }

    [Test]
    public async Task Subscription_changes_take_effect_on_the_next_step_and_can_be_removed()
    {
        var calls = new List<string>();
        Func<Step, Task> added = _ => { calls.Add("added"); return Task.CompletedTask; };
        var registered = false;
        Func<Step, Task> before = _ =>
        {
            if (!registered)
            {
                registered = true;
                BDTestSettings.AfterStep += added;
            }
            return Task.CompletedTask;
        };
        BDTestSettings.BeforeStep += before;
        try
        {
            await new HookTest().When(() => calls.Add("when")).Then(() => calls.Add("then")).BDTestAsync();
            Assert.That(calls, Is.EqualTo(new[] { "when", "then", "added" }));
        }
        finally
        {
            BDTestSettings.BeforeStep -= before;
            BDTestSettings.AfterStep -= added;
        }
        calls.Clear();
        await new HookTest().When(() => NoOp()).Then(() => NoOp()).BDTestAsync();
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
            var test = new HookTest
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
        var test = new HookTest
        {
            Before = async _ => { await Console.Out.WriteLineAsync("before output"); },
            After = async step =>
            {
                outputs.Add(step.Output);
                await Console.Out.WriteLineAsync("after output");
            }
        };
        var scenario = await test.When(() => Console.WriteLine("body output")).Then(() => NoOp()).BDTestAsync();
        Assert.That(outputs[0], Does.Contain("before output").And.Contain("body output").And.Not.Contain("after output"));
        Assert.That(outputs[1], Is.EqualTo("before output"));
        Assert.That(scenario.Steps[0].Output, Does.Contain("after output"));
        Assert.That(scenario.Output, Does.Contain("body output").And.Contain("after output"));
    }

    private static Scenario GetScenario(Step step) => BDTestUtil.GetScenarios().Single(scenario => scenario.Guid == step.ScenarioId);
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

    private sealed class HookTest : BDTestBase
    {
        private readonly string _id = Guid.NewGuid().ToString("N");
        protected override string BDTestExecutionId => _id;
        public string ExecutionId => _id;
        public Func<Step, Task> Before { get; init; } = _ => Task.CompletedTask;
        public Func<Step, Task> After { get; init; } = _ => Task.CompletedTask;
        public override Task OnBeforeStep(Step step) => Before(step);
        public override Task OnAfterStep(Step step) => After(step);
    }
}
