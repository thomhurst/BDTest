using System.Threading.Tasks.Sources;
using BDTest;
using BDTest.Test;
using BDTest.Tests.Helpers;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[NotInParallel]
public class DelegateStepTests : RunTestBase
{
    [Test]
    public async Task Every_continuation_accepts_Task_ValueTask_and_generic_ValueTask_delegates()
    {
        var calls = 0;
        Action action = () => calls++;
        Func<Task> task = async () => { await Task.Yield(); calls++; };
        Func<ValueTask> valueTask = async () => { await Task.Yield(); calls++; };
        Func<ValueTask<int>> result = async () => { await Task.Yield(); return ++calls; };
        Func<CancellationToken, Task> tokenTask = async token => { await Task.Yield(); calls++; };
        Func<CancellationToken, ValueTask> tokenValueTask = async token => { await Task.Yield(); calls++; };
        Func<CancellationToken, ValueTask<int>> tokenResult = async token => { await Task.Yield(); return ++calls; };

        var scenario = await Given(valueTask).And(result).But(tokenResult).And(task)
            .But(tokenTask).When(tokenValueTask).But(result).Then(tokenResult)
            .And(valueTask).But(tokenValueTask).And(action).BDTestAsync();
        await Assert.That(calls).IsEqualTo(11);
        await Assert.That(scenario.Status).IsEqualTo(Status.Passed);

        await Given(tokenResult).When(result).Then(valueTask).BDTestAsync();
        await When(result).Then(tokenValueTask).And(result).BDTestAsync();
        await Assert.That(calls).IsEqualTo(17);
    }

    [Test]
    public async Task Explicit_generic_root_and_continuation_calls_preserve_generated_skip_attributes()
    {
        Options.SkipStepRules.Add<SkipDelegateStepAttribute>(_ => true);
        var given = await Given<int>(() => SkippedValueTask()).When(() => Accept(42)).Then(() => Accept(42)).BDTestAsync();
        var when = await When<int>(() => SkippedValueTask()).Then(() => Accept(42)).BDTestAsync();
        var then = await When(() => Accept(42)).Then<int>(() => SkippedValueTask()).BDTestAsync();
        await Assert.That(Required(given.Steps)[0].StepText).IsEqualTo("[Skipped] Given explicit generic step");
        await Assert.That(Required(when.Steps)[0].StepText).IsEqualTo("[Skipped] When explicit generic step");
        await Assert.That(Required(then.Steps)[1].StepText).IsEqualTo("[Skipped] Then explicit generic step");
        await Assert.That(new[] { given.Steps[0], when.Steps[0], then.Steps[1] }.All(step => step.Status == Status.Skipped)).IsTrue();
    }

    [Test]
    public async Task Ordinary_async_lambdas_bind_without_casts_and_preserve_order()
    {
        var calls = new List<int>();
        await Given(async () => { await Task.Yield(); calls.Add(1); })
            .And(async token => { await Task.Yield(); calls.Add(2); })
            .When(async () => { await Task.Yield(); calls.Add(3); })
            .But(async token => { await Task.Yield(); calls.Add(4); })
            .Then(async () => { await Task.Yield(); calls.Add(5); })
            .And(async token => { await Task.Yield(); calls.Add(6); }).BDTestAsync();
        await Assert.That(calls).IsEquivalentTo(Enumerable.Range(1, 6), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Statement_bodies_null_conditionals_and_patterns_keep_meaningful_caller_text()
    {
        List<int>? values = null;
        var scenario = await Given(() => { values = new List<int>(); })
            .When(() => values?.Add(42))
            .Then(async () => { await Assert.That(values is { Count: > 0 }).IsTrue(); }).BDTestAsync();
        await Assert.That(Required(scenario.Steps)[0].StepText).Contains("values = new List<int>()");
        await Assert.That(scenario.Steps[1].StepText).Contains("values?.Add(42)");
        await Assert.That(scenario.Steps[2].StepText).Contains("Count: > 0");
    }

    [Test]
    public async Task Without_generator_metadata_caller_text_and_explicit_arguments_still_work()
    {
        var value = 1;
        var reads = 0;
        var builder = When(() => { value = 42; }, callerFile: "no-catalog.cs", callerLine: 1)
            .Then(() => Accept(value), callerFile: "no-catalog.cs", callerLine: 2)
            .WithArgs(() => { reads++; return value; });
        await Assert.That(reads).IsZero();
        var scenario = await builder.BDTestAsync();
        await Assert.That(reads).IsEqualTo(1);
        await Assert.That(Required(scenario.Steps)[1].StepText).IsEqualTo("Then () => Accept(value) 42");
    }

    [Test]
    public async Task ValueTask_source_is_consumed_exactly_once()
    {
        var source = new SingleUseSource();
        var execution = When(() => source.Create()).Then(() => Accept(42)).BDTestAsync();
        await Assert.That(execution.IsCompleted).IsFalse();
        source.Complete();
        await execution.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(source.ResultsRead).IsEqualTo(1);
    }

    [Test]
    [Arguments(false), Arguments(true)]
    public async Task Cancellation_aware_ValueTask_steps_receive_and_observe_timeout_token(bool generic)
    {
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async ValueTask Wait(CancellationToken token)
        {
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            finally { if (token.IsCancellationRequested) cancelled.TrySetResult(); }
        }
        async ValueTask<int> WaitResult(CancellationToken token) { await Wait(token); return 42; }
        var builder = (generic ? When(token => WaitResult(token)) : When(token => Wait(token)))
            .WithStepTimeout(TimeSpan.FromMilliseconds(150)).Then(() => Accept(42));
        await Assert.That(async () => await builder.BDTestAsync()).ThrowsExactly<TimeoutException>();
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task Cancellation_aware_generated_metadata_omits_token_and_renders_other_arguments()
    {
        var scenario = await When(token => AcceptToken(42, token)).Then(() => Accept(42)).BDTestAsync();
        await Assert.That(Required(scenario.Steps)[0].StepText).IsEqualTo("When received 42");
    }

    [Test]
    public async Task Failure_output_names_the_failing_statement_delegate()
    {
        var builder = When(() => { throw new InvalidOperationException("delegate failure"); }).Then(() => Accept(42));
        await Assert.That(async () => await builder.BDTestAsync()).ThrowsExactly<InvalidOperationException>();
        var step = Required(BDTestUtil.GetScenarios(TestRun).Single().Steps)[0];

        await Assert.That(step.Output).Contains("delegate failure").And.Contains("executing").And.Contains("When");
    }

    private sealed class SkipDelegateStepAttribute : SkipStepAttribute { }

    [SkipDelegateStep, StepText("explicit generic step")]
    private static ValueTask<int> SkippedValueTask() => throw new InvalidOperationException("Skipped method must not run");

    private static void Accept(int value) { }

    [StepText("received {value}")]
    private static ValueTask AcceptToken(int value, CancellationToken token) => default;

    private sealed class SingleUseSource : IValueTaskSource<int>
    {
        private ManualResetValueTaskSourceCore<int> _source = new() { RunContinuationsAsynchronously = true };
        public int ResultsRead { get; private set; }
        public ValueTask<int> Create() => new(this, _source.Version);
        public void Complete() => _source.SetResult(42);
        public int GetResult(short token)
        {
            if (++ResultsRead > 1) throw new InvalidOperationException("ValueTask consumed twice");
            return _source.GetResult(token);
        }
        public ValueTaskSourceStatus GetStatus(short token) => _source.GetStatus(token);
        public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
            => _source.OnCompleted(continuation, state, token, flags);
    }
}
