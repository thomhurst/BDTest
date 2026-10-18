using BDTest.Attributes;
using BDTest.Test;
using Newtonsoft.Json;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class TimeoutTests : BDTestBase
{
    [SetUp]
    public void BindMetadata() => BeginTest(TestContext.CurrentContext.Test.ID, Required(global::NUnit.Framework.Internal.TestExecutionContext.CurrentContext.CurrentTest.Method).MethodInfo);

    [TearDown]
    public void FinishExecution() => EndTest();

    private readonly string _executionId = Guid.NewGuid().ToString();
    protected override string BDTestExecutionId => _executionId;
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(150);
    private readonly TaskCompletionSource<bool> _cancelled = NewSignal();
    private readonly TaskCompletionSource<bool> _finish = NewSignal();
    private readonly List<CancellationToken> _tokens = new();
    private int _laterSteps;

    [Test]
    public async Task Step_timeout_cancels_token_and_records_failure()
    {
        var builder = When(token => WaitForCancellation(token)).WithStepTimeout(Timeout).Then(() => Later());
        var exception = await Assert.ThrowsAsync<TimeoutException>(async () => await builder.BDTestAsync().WaitAsync(TimeSpan.FromSeconds(10)));
        await _cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var scenario = Report(builder);
        Assert.That(Required(exception).Message, Does.Contain("Step").And.Contain("timed out"));
        Assert.That(scenario.Status, Is.EqualTo(Status.Failed));
        Assert.That(scenario.TimedOut, Is.True);
        Assert.That(Required(scenario.Steps)[0].TimedOut, Is.True);
        Assert.That(scenario.Steps[0].Status, Is.EqualTo(Status.Failed));
        Assert.That(Required(scenario.Steps[0].Exception).Message, Is.EqualTo(Required(exception).Message));
        Assert.That(scenario.Steps[0].EndTime, Is.GreaterThan(scenario.Steps[0].StartTime));
        Assert.That(scenario.Steps[1].Status, Is.EqualTo(Status.NotRun));
        Assert.That(_laterSteps, Is.Zero);
        var restored = ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(scenario));
        Assert.That(restored.TimedOut, Is.True);
        Assert.That(Required(restored.Steps)[0].TimedOut, Is.True);
    }

    [TestCase("synchronous")]
    [TestCase("async")]
    [TestCase("async prefix")]
    public async Task Timeout_returns_when_work_ignores_cancellation_even_before_returning_a_task(string kind)
    {
        var builder = kind switch
        {
            "synchronous" => When(() => Block()).WithStepTimeout(Timeout).Then(() => Later()),
            "async prefix" => When(token => BlockBeforeReturningTask(token)).WithStepTimeout(Timeout).Then(() => Later()),
            _ => When(() => WaitWithoutCancellation()).WithStepTimeout(Timeout).Then(() => Later())
        };
        try
        {
            await Assert.ThrowsAsync<TimeoutException>(async () => await builder.BDTestAsync().WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.That(Report(builder).TimedOut, Is.True);
            Assert.That(_laterSteps, Is.Zero);
        }
        finally
        {
            _finish.TrySetResult(true);
        }
    }

    [Test]
    public async Task Scenario_timeout_uses_one_budget_across_steps_and_overrides_longer_step_timeout()
    {
        var builder = Given(token => CompleteAndSpendBudget(token))
            .When(token => WaitForCancellation(token)).WithStepTimeout(TimeSpan.FromSeconds(30))
            .Then(() => Later()).WithScenarioTimeout(TimeSpan.FromSeconds(1));
        await Assert.ThrowsAsync<TimeoutException>(async () => await builder.BDTestAsync().WaitAsync(TimeSpan.FromSeconds(10)));
        await _cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var scenario = Report(builder);
        Assert.That(Required(scenario.Steps)[0].Status, Is.EqualTo(Status.Passed));
        Assert.That(Required(scenario.Steps[1].Exception).Message, Does.StartWith("Scenario").And.Contain("00:00:01"));
        Assert.That(scenario.Steps[1].TimeTaken, Is.LessThan(TimeSpan.FromSeconds(1)));
        Assert.That(_laterSteps, Is.Zero);
    }

    [Test]
    public async Task Token_overloads_work_at_every_chain_position_and_preserve_text()
    {
        var scenario = await Given(token => Complete(token)).And(token => Complete(token))
            .When(token => Complete(token)).Then(token => Complete(token)).And(token => Complete(token)).BDTestAsync();
        Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
        Assert.That(scenario.TimedOut, Is.False);
        Assert.That(_tokens, Has.Count.EqualTo(5));
        Assert.That(_tokens.All(token => !token.CanBeCanceled), Is.True);
        Assert.That(Required(scenario.Steps).Select(step => step.StepText), Is.EqualTo(new[] { "Given complete", "And complete", "When complete", "Then complete", "And complete" }));
        var second = await Given(token => Complete(token)).When(token => Complete(token)).Then(() => Later()).BDTestAsync();
        Assert.That(second.Status, Is.EqualTo(Status.Passed));
    }

    [Test]
    public async Task Token_overloads_work_through_But_steps_and_keep_timeout_configuration()
    {
        var scenario = await Given(token => Complete(token)).But(token => Complete(token))
            .And(token => Complete(token)).But(token => Complete(token))
            .When(token => Complete(token)).But(token => Complete(token))
            .Then(token => Complete(token)).But(token => Complete(token))
            .And(token => Complete(token)).But(token => Complete(token))
            .WithStepTimeout(TimeSpan.FromSeconds(10)).WithScenarioTimeout(TimeSpan.FromSeconds(10)).BDTestAsync();

        Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
        Assert.That(_tokens, Has.Count.EqualTo(10));
        Assert.That(_tokens.All(token => token.CanBeCanceled), Is.True);
        Assert.That(Required(scenario.Steps).Select(step => step.StepText), Is.EqualTo(new[]
        {
            "Given complete", "But complete", "And complete", "But complete", "When complete",
            "But complete", "Then complete", "But complete", "And complete", "But complete"
        }));
        Assert.That(scenario.Steps.Last().Timeout, Is.EqualTo(TimeSpan.FromSeconds(10)));
    }

    [Test]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("BDTest", "BDT001", Justification = "Null steps must throw during construction.")]
    public void Null_token_expressions_fail_at_construction()
    {
        // Deliberately invalid delegate exercises every timeout-aware builder guard.
        Func<CancellationToken, Task> missing = null!;
        Assert.That(Required(Assert.Throws<ArgumentNullException>(() => Given(missing))).ParamName, Is.EqualTo("step"));
        Assert.That(Required(Assert.Throws<ArgumentNullException>(() => When(missing))).ParamName, Is.EqualTo("step"));
        var prefix = Given(token => Complete(token));
        Assert.That(Required(Assert.Throws<ArgumentNullException>(() => prefix.But(missing))).ParamName, Is.EqualTo("step"));
    }

    [Test]
    public async Task Configured_success_and_user_cancellation_keep_existing_classification()
    {
        var success = await When(token => Complete(token)).WithStepTimeout(TimeSpan.FromSeconds(10))
            .Then(() => Later()).WithScenarioTimeout(TimeSpan.FromSeconds(10)).BDTestAsync();
        Assert.That(success.Status, Is.EqualTo(Status.Passed));
        Assert.That(success.TimedOut, Is.False);
        var builder = When(token => CancelIndependently()).WithStepTimeout(TimeSpan.FromSeconds(10)).Then(() => Later());
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await builder.BDTestAsync());
        Assert.That(Report(builder).TimedOut, Is.False);
        Assert.That(Report(builder).Status, Is.EqualTo(Status.Failed));
    }

    [Test]
    [BDTestRetry(1)]
    public async Task Retry_uses_fresh_token_and_clears_timeout_results()
    {
        var scenario = await When(token => TimeoutOnce(token)).WithStepTimeout(Timeout).Then(() => Later()).BDTestAsync();
        Assert.That(scenario.RetryCount, Is.EqualTo(1));
        Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
        Assert.That(scenario.TimedOut, Is.False);
        Assert.That(Required(scenario.Steps)[0].Exception, Is.Null);
        Assert.That(_tokens, Has.Count.EqualTo(2));
        Assert.That(_tokens[0], Is.Not.EqualTo(_tokens[1]));
        Assert.That(_tokens[1].IsCancellationRequested, Is.False);
    }

    [Test]
    public async Task Builder_continuations_copy_timeouts_without_sharing_configuration()
    {
        var prefix = When(token => Complete(token)).WithStepTimeout(TimeSpan.FromSeconds(10)).WithScenarioTimeout(TimeSpan.FromSeconds(10));
        var fast = prefix.Then(() => Later());
        var slow = prefix.Then(token => WaitForCancellation(token)).WithStepTimeout(Timeout).WithScenarioTimeout(TimeSpan.FromSeconds(20));
        var success = await fast.BDTestAsync();
        await Assert.ThrowsAsync<TimeoutException>(async () => await slow.BDTestAsync().WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.That(success.Timeout, Is.EqualTo(TimeSpan.FromSeconds(10)));
        Assert.That(Required(success.Steps)[0].Timeout, Is.EqualTo(TimeSpan.FromSeconds(10)));
        Assert.That(Report(slow).Timeout, Is.EqualTo(TimeSpan.FromSeconds(20)));
        Assert.That(success.TimedOut, Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Cancellation_callbacks_cannot_block_or_replace_timeout_failure(bool throws)
    {
        var builder = When(token => WaitWithCallback(token, throws)).WithStepTimeout(Timeout).Then(() => Later());
        try
        {
            await Assert.ThrowsAsync<TimeoutException>(async () => await builder.BDTestAsync().WaitAsync(TimeSpan.FromSeconds(10)));
            await _cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.That(Report(builder).TimedOut, Is.True);
        }
        finally
        {
            _finish.TrySetResult(true);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Abandoned_execution_is_observed_while_cancellation_callback_is_blocked(bool callbackThrows)
    {
        var cancellation = new CancellationTokenSource();
        var entered = NewSignal();
        var released = NewSignal();
        var execution = NewSignal();
        using var registration = cancellation.Token.Register(() =>
        {
            entered.TrySetResult(true);
            _finish.Task.GetAwaiter().GetResult();
            released.TrySetResult(true);
            if (callbackThrows) { throw new InvalidOperationException("callback failure"); }
        });
        var observation = Step.CancelAndObserve(execution.Task, cancellation);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            execution.SetException(new InvalidOperationException("late execution failure"));
            await observation.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.That(released.Task.IsCompleted, Is.False);
        }
        finally
        {
            _finish.TrySetResult(true);
            await released.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await observation.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Late_completion_does_not_change_timeout_results(bool throws)
    {
        var completed = NewSignal();
        var builder = When(() => FinishLate(completed, throws)).WithStepTimeout(Timeout).Then(() => Later());
        try
        {
            await Assert.ThrowsAsync<TimeoutException>(async () => await builder.BDTestAsync().WaitAsync(TimeSpan.FromSeconds(10)));
        }
        finally
        {
            _finish.TrySetResult(true);
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        var scenario = Report(builder);
        Assert.That(scenario.Status, Is.EqualTo(Status.Failed));
        Assert.That(Required(scenario.Steps)[0].Status, Is.EqualTo(Status.Failed));
        Assert.That(Required(scenario.Steps[0].Exception).Message, Does.Contain("timed out"));
        Assert.That(_laterSteps, Is.Zero);
    }

    [Test]
    public async Task Timed_steps_preserve_output_and_step_text_attributes()
    {
        var scenario = await When(token => WriteOutput("value", token)).WithStepTimeout(TimeSpan.FromSeconds(10))
            .Then(() => Later()).BDTestAsync();
        Assert.That(Required(scenario.Steps)[0].StepText, Is.EqualTo("When writes value"));
        Assert.That(scenario.Steps[0].Output, Does.Contain("step output"));
    }

    [Test]
    public async Task Existing_parameterless_expressions_keep_cancellation_arguments_in_step_text()
    {
        var scenario = await When(() => ExplicitTokenText(CancellationToken.None)).Then(() => Later()).BDTestAsync();
        Assert.That(Required(scenario.Steps)[0].StepText, Is.EqualTo("When token System.Threading.CancellationToken"));
    }

    [TestCase(0)]
    [TestCase(-2)]
    [TestCase(2147483648d)]
    public void Invalid_timeouts_are_rejected_when_configured(double milliseconds)
    {
        var builder = When(() => Later());
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithStepTimeout(TimeSpan.FromMilliseconds(milliseconds)));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithScenarioTimeout(TimeSpan.FromMilliseconds(milliseconds)));
    }

    private static Scenario Report(BuildableTest builder) => BDTestUtil.GetScenarios().Single(scenario => scenario.Guid == builder.Guid);
    private void Later() => _laterSteps++;
    private Task Complete(CancellationToken token) { _tokens.Add(token); return Task.CompletedTask; }
    private static Task CancelIndependently() => throw new OperationCanceledException("User cancellation");
    private void Block() => _finish.Task.GetAwaiter().GetResult();
    private Task WaitWithoutCancellation() => _finish.Task;
    private Task BlockBeforeReturningTask(CancellationToken token)
    {
        Block();
        return Task.CompletedTask;
    }
    private static async Task CompleteAndSpendBudget(CancellationToken token) => await Task.Delay(600, token);
    private Task TimeoutOnce(CancellationToken token)
    {
        _tokens.Add(token);
        return _tokens.Count == 1 ? WaitForCancellation(token) : Task.CompletedTask;
    }
    private async Task WaitForCancellation(CancellationToken token)
    {
        try { await Task.Delay(System.Threading.Timeout.Infinite, token); }
        finally { _cancelled.TrySetResult(true); }
    }
    private async Task WaitWithCallback(CancellationToken token, bool throws)
    {
        using var registration = token.Register(() =>
        {
            _cancelled.TrySetResult(true);
            _finish.Task.GetAwaiter().GetResult();
            if (throws) { throw new InvalidOperationException("Cancellation callback failure"); }
        });
        await Task.Delay(System.Threading.Timeout.Infinite, token);
    }
    private async Task FinishLate(TaskCompletionSource<bool> completed, bool throws)
    {
        try
        {
            await _finish.Task;
            if (throws) { throw new InvalidOperationException("Late failure"); }
        }
        finally { completed.TrySetResult(true); }
    }
    [StepText("token {0}")]
    private static Task ExplicitTokenText(CancellationToken token) => Task.CompletedTask;

    [StepText("writes {0}")]
    private static Task WriteOutput(string value, CancellationToken token)
    {
        Console.WriteLine("step output");
        return Task.CompletedTask;
    }
    private static TaskCompletionSource<bool> NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
