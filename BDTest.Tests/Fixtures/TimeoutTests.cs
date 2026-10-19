using BDTest;
using BDTest.Test;
using Newtonsoft.Json;
using Microsoft.Extensions.Time.Testing;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;
public class TimeoutTests : RunTestBase
{
    protected override bool BindCurrentTestMetadata => true;

    [After(HookType.Test)]
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
        var exception = await Assert.That(async () => await builder.BDTestAsync().WaitAsync(TimeSpan.FromSeconds(10))).ThrowsExactly<TimeoutException>();
        await _cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var scenario = Report(builder);

        await Assert.That(Required(exception).Message).Contains("Step").And.Contains("timed out");
        await Assert.That(scenario.Status).IsEqualTo(Status.Failed);
        await Assert.That(scenario.TimedOut).IsTrue();
        await Assert.That(Required(scenario.Steps)[0].TimedOut).IsTrue();
        await Assert.That(scenario.Steps[0].Status).IsEqualTo(Status.Failed);
        await Assert.That(Required(scenario.Steps[0].Exception).Message).IsEqualTo(Required(exception).Message);
        await Assert.That(scenario.Steps[0].EndTime).IsGreaterThan(scenario.Steps[0].StartTime);
        await Assert.That(scenario.Steps[1].Status).IsEqualTo(Status.NotRun);
        await Assert.That(_laterSteps).IsZero();
        var restored = ReportJson.DeserializeObject<BDTest.Contracts.ScenarioResult>(ReportJson.SerializeObject(scenario));
        await Assert.That(restored.TimedOut).IsTrue();
        await Assert.That(Required(restored.Steps)[0].TimedOut).IsTrue();
    }

    [Test]
    [Arguments("synchronous")]
    [Arguments("async")]
    [Arguments("async prefix")]
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
            await Assert.That(async () => await builder.BDTestAsync().WaitAsync(TimeSpan.FromSeconds(10))).ThrowsExactly<TimeoutException>();
            await Assert.That(Report(builder).TimedOut).IsTrue();
            await Assert.That(_laterSteps).IsZero();
        }
        finally
        {
            _finish.TrySetResult(true);
        }
    }

    [Test]
    // Preserve the wall-clock probe's budget independently of unrelated parallel work.
    [NotInParallel]
    public async Task Scenario_timeout_uses_one_budget_across_steps_and_overrides_longer_step_timeout()
    {
        var builder = Given(token => CompleteAndSpendBudget(token))
            .When(token => WaitForCancellation(token)).WithStepTimeout(TimeSpan.FromSeconds(30))
            .Then(() => Later()).WithScenarioTimeout(TimeSpan.FromSeconds(1));
        await Assert.That(async () => await builder.BDTestAsync().WaitAsync(TimeSpan.FromSeconds(10))).ThrowsExactly<TimeoutException>();
        await _cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var scenario = Report(builder);
        await Assert.That(Required(scenario.Steps)[0].Status).IsEqualTo(Status.Passed);

        await Assert.That(Required(scenario.Steps[1].Exception).Message).StartsWith("Scenario").And.Contains("00:00:01");
        await Assert.That(scenario.Steps[1].TimeTaken).IsLessThan(TimeSpan.FromSeconds(1));
        await Assert.That(_laterSteps).IsZero();
    }

    [Test]
    public async Task Token_overloads_work_at_every_chain_position_and_preserve_text()
    {
        var scenario = await Given(token => Complete(token)).And(token => Complete(token))
            .When(token => Complete(token)).Then(token => Complete(token)).And(token => Complete(token)).BDTestAsync();
        await Assert.That(scenario.Status).IsEqualTo(Status.Passed);
        await Assert.That(scenario.TimedOut).IsFalse();
        await Assert.That(_tokens).Count().IsEqualTo(5);
        await Assert.That(_tokens.All(token => token.CanBeCanceled)).IsTrue();
        await Assert.That(Required(scenario.Steps).Select(step => step.StepText)).IsEquivalentTo((new[] { "Given complete", "And complete", "When complete", "Then complete", "And complete" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        var second = await Given(token => Complete(token)).When(token => Complete(token)).Then(() => Later()).BDTestAsync();
        await Assert.That(second.Status).IsEqualTo(Status.Passed);
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

        await Assert.That(scenario.Status).IsEqualTo(Status.Passed);
        await Assert.That(_tokens).Count().IsEqualTo(10);
        await Assert.That(_tokens.All(token => token.CanBeCanceled)).IsTrue();
        await Assert.That(Required(scenario.Steps).Select(step => step.StepText)).IsEquivalentTo((new[]
        {
            "Given complete", "But complete", "And complete", "But complete", "When complete",
            "But complete", "Then complete", "But complete", "And complete", "But complete"
        }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(scenario.Steps.Last().Timeout).IsEqualTo(TimeSpan.FromSeconds(10));
    }

    [Test]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("BDTest", "BDT001", Justification = "Null steps must throw during construction.")]
    public async Task Null_token_expressions_fail_at_construction()
    {
        // Deliberately invalid delegate exercises every timeout-aware builder guard.
        Func<CancellationToken, Task> missing = null!;
        await Assert.That(Required(await Assert.That(() => Given(missing)).ThrowsExactly<ArgumentNullException>()).ParamName).IsEqualTo("step");
        await Assert.That(Required(await Assert.That(() => When(missing)).ThrowsExactly<ArgumentNullException>()).ParamName).IsEqualTo("step");
        var prefix = Given(token => Complete(token));
        await Assert.That(Required(await Assert.That(() => prefix.But(missing)).ThrowsExactly<ArgumentNullException>()).ParamName).IsEqualTo("step");
    }

    [Test]
    public async Task Configured_success_and_user_cancellation_keep_existing_classification()
    {
        var success = await When(token => Complete(token)).WithStepTimeout(TimeSpan.FromSeconds(10))
            .Then(() => Later()).WithScenarioTimeout(TimeSpan.FromSeconds(10)).BDTestAsync();
        await Assert.That(success.Status).IsEqualTo(Status.Passed);
        await Assert.That(success.TimedOut).IsFalse();
        var builder = When(token => CancelIndependently()).WithStepTimeout(TimeSpan.FromSeconds(10)).Then(() => Later());
        await Assert.That(async () => await builder.BDTestAsync()).ThrowsExactly<OperationCanceledException>();
        await Assert.That(Report(builder).TimedOut).IsFalse();
        await Assert.That(Report(builder).Status).IsEqualTo(Status.Failed);
    }

    [Test]
    [BDTestRetry(1)]
    public async Task Retry_uses_fresh_token_and_clears_timeout_results()
    {
        var clock = new TimeoutClock();
        Options.TimeProvider = clock;
        var scenario = await When(token => TimeoutOnce(token, clock)).WithStepTimeout(Timeout).Then(() => Later()).BDTestAsync()
            .WaitAsync(TimeSpan.FromSeconds(10)); // Deadlock guard only; the step deadline uses virtual time.
        await _cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var attempts = Required(scenario.Attempts);
        await Assert.That(attempts).Count().IsEqualTo(2);
        await Assert.That(attempts[0].Status).IsEqualTo(Status.Failed);
        await Assert.That(attempts[0].Steps[0].TimedOut).IsTrue();
        await Assert.That(Required(attempts[0].Steps[0].Exception).Message).StartsWith("Step timed out");
        await Assert.That(attempts[0].Steps[0].TimeTaken).IsEqualTo(Timeout);
        await Assert.That(attempts[0].Steps[1].Status).IsEqualTo(Status.NotRun);
        await Assert.That(attempts[1].Status).IsEqualTo(Status.Passed);
        await Assert.That(attempts[1].Steps[0].TimedOut).IsFalse();
        await Assert.That(attempts[1].Steps[0].Exception).IsNull();
        await Assert.That(scenario.RetryCount).IsEqualTo(1);
        await Assert.That(scenario.Status).IsEqualTo(Status.Passed);
        await Assert.That(scenario.TimedOut).IsFalse();
        await Assert.That(scenario.Exception).IsNull();
        await Assert.That(Required(scenario.Steps)[0].TimedOut).IsFalse();
        await Assert.That(scenario.Steps[0].Exception).IsNull();
        await Assert.That(_laterSteps).IsEqualTo(1);
        await Assert.That(_tokens).Count().IsEqualTo(2);
        await Assert.That(_tokens.All(token => token.CanBeCanceled)).IsTrue();
        await Assert.That(_tokens[0].IsCancellationRequested).IsTrue();
        await Assert.That(_tokens[0]).IsNotEqualTo(_tokens[1]);
        await Assert.That(_tokens[1].IsCancellationRequested).IsFalse();
    }

    [Test]
    public async Task Builder_continuations_copy_timeouts_without_sharing_configuration()
    {
        var prefix = When(token => Complete(token)).WithStepTimeout(TimeSpan.FromSeconds(10)).WithScenarioTimeout(TimeSpan.FromSeconds(10));
        var fast = prefix.Then(() => Later());
        var slow = prefix.Then(token => WaitForCancellation(token)).WithStepTimeout(Timeout).WithScenarioTimeout(TimeSpan.FromSeconds(20));
        var success = await fast.BDTestAsync();
        await Assert.That(async () => await slow.BDTestAsync().WaitAsync(TimeSpan.FromSeconds(10))).ThrowsExactly<TimeoutException>();
        await Assert.That(success.Timeout).IsEqualTo(TimeSpan.FromSeconds(10));
        await Assert.That(Required(success.Steps)[0].Timeout).IsEqualTo(TimeSpan.FromSeconds(10));
        await Assert.That(Report(slow).Timeout).IsEqualTo(TimeSpan.FromSeconds(20));
        await Assert.That(success.TimedOut).IsFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Cancellation_callbacks_cannot_block_or_replace_timeout_failure(bool throws)
    {
        var builder = When(token => WaitWithCallback(token, throws)).WithStepTimeout(Timeout).Then(() => Later());
        try
        {
            await Assert.That(async () => await builder.BDTestAsync().WaitAsync(TimeSpan.FromSeconds(10))).ThrowsExactly<TimeoutException>();
            await _cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(Report(builder).TimedOut).IsTrue();
        }
        finally
        {
            _finish.TrySetResult(true);
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
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
            await Assert.That(released.Task.IsCompleted).IsFalse();
        }
        finally
        {
            _finish.TrySetResult(true);
            await released.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await observation.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Late_completion_does_not_change_timeout_results(bool throws)
    {
        var completed = NewSignal();
        var builder = When(() => FinishLate(completed, throws)).WithStepTimeout(Timeout).Then(() => Later());
        try
        {
            await Assert.That(async () => await builder.BDTestAsync().WaitAsync(TimeSpan.FromSeconds(10))).ThrowsExactly<TimeoutException>();
        }
        finally
        {
            _finish.TrySetResult(true);
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        var scenario = Report(builder);
        await Assert.That(scenario.Status).IsEqualTo(Status.Failed);
        await Assert.That(Required(scenario.Steps)[0].Status).IsEqualTo(Status.Failed);
        await Assert.That(Required(scenario.Steps[0].Exception).Message).Contains("timed out");
        await Assert.That(_laterSteps).IsZero();
    }

    [Test]
    public async Task Timed_steps_preserve_output_and_step_text_attributes()
    {
        var scenario = await When(token => WriteOutput("value", token)).WithStepTimeout(TimeSpan.FromSeconds(10))
            .Then(() => Later()).BDTestAsync();
        await Assert.That(Required(scenario.Steps)[0].StepText).IsEqualTo("When writes value");
        await Assert.That(scenario.Steps[0].Output).Contains("step output");
    }

    [Test]
    public async Task Parameterless_delegates_also_omit_cancellation_arguments()
    {
        var scenario = await When(() => ExplicitTokenText(CancellationToken.None)).Then(() => Later()).BDTestAsync();
        await Assert.That(Required(scenario.Steps)[0].StepText).IsEqualTo("When token ");
    }

    [Test]
    [Arguments(0)]
    [Arguments(-2)]
    [Arguments(2147483648d)]
    public async Task Invalid_timeouts_are_rejected_when_configured(double milliseconds)
    {
        var builder = When(() => Later());
        await Assert.That(() => builder.WithStepTimeout(TimeSpan.FromMilliseconds(milliseconds))).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => builder.WithScenarioTimeout(TimeSpan.FromMilliseconds(milliseconds))).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    private BDTest.Contracts.ScenarioResult Report(BuildableTest builder) => BDTestUtil.GetScenarios(TestRun).Single(scenario => scenario.Guid == builder.Guid);
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
    private async Task TimeoutOnce(CancellationToken token, TimeoutClock clock)
    {
        _tokens.Add(token);
        if (_tokens.Count == 1)
        {
            // Task.Run may enter the step before WaitAsync creates its timer. Advance
            // only after it is armed, and leave time frozen throughout the retry.
            await clock.TimerCreated.Task;
            clock.Fake.Advance(Timeout);
            await WaitForCancellation(token);
        }
    }

    private sealed class TimeoutClock : TimeProvider
    {
        internal FakeTimeProvider Fake { get; } = new();
        internal TaskCompletionSource<bool> TimerCreated { get; } = NewSignal();
        public override DateTimeOffset GetUtcNow() => Fake.GetUtcNow();
        public override long GetTimestamp() => Fake.GetTimestamp();
        public override long TimestampFrequency => Fake.TimestampFrequency;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = Fake.CreateTimer(callback, state, dueTime, period);
            TimerCreated.TrySetResult(true);
            return timer;
        }
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
    #pragma warning disable BDT002 // Token placeholders exercise runtime omission.
    [StepText("token {token}")]
    private static Task ExplicitTokenText(CancellationToken token) => Task.CompletedTask;
#pragma warning restore BDT002

    [StepText("writes {value}")]
    private static Task WriteOutput(string value, CancellationToken token)
    {
        Console.WriteLine("step output");
        return Task.CompletedTask;
    }
    private static TaskCompletionSource<bool> NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
