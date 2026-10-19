using System.Threading.Tasks.Sources;
using BDTest.Attributes;
using BDTest.Test;
using BDTest.Tests.Helpers;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
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
        Assert.That(calls, Is.EqualTo(11));
        Assert.That(scenario.Status, Is.EqualTo(Status.Passed));

        await Given(tokenResult).When(result).Then(valueTask).BDTestAsync();
        await When(result).Then(tokenValueTask).And(result).BDTestAsync();
        Assert.That(calls, Is.EqualTo(17));
    }

    [Test]
    public async Task Explicit_generic_root_and_continuation_calls_preserve_generated_skip_attributes()
    {
        Options.SkipStepRules.Add<SkipDelegateStepAttribute>(_ => true);
        var given = await Given<int>(() => SkippedValueTask()).When(() => Accept(42)).Then(() => Accept(42)).BDTestAsync();
        var when = await When<int>(() => SkippedValueTask()).Then(() => Accept(42)).BDTestAsync();
        var then = await When(() => Accept(42)).Then<int>(() => SkippedValueTask()).BDTestAsync();
        Assert.That(Required(given.Steps)[0].StepText, Is.EqualTo("[Skipped] Given explicit generic step"));
        Assert.That(Required(when.Steps)[0].StepText, Is.EqualTo("[Skipped] When explicit generic step"));
        Assert.That(Required(then.Steps)[1].StepText, Is.EqualTo("[Skipped] Then explicit generic step"));
        Assert.That(new[] { given.Steps[0], when.Steps[0], then.Steps[1] }.All(step => step.Status == Status.Skipped), Is.True);
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
        Assert.That(calls, Is.EqualTo(Enumerable.Range(1, 6)));
    }

    [Test]
    public async Task Statement_bodies_null_conditionals_and_patterns_keep_meaningful_caller_text()
    {
        List<int>? values = null;
        var scenario = await Given(() => { values = new List<int>(); })
            .When(() => values?.Add(42))
            .Then(() => { Assert.That(values is { Count: > 0 }, Is.True); }).BDTestAsync();
        Assert.That(Required(scenario.Steps)[0].StepText, Does.Contain("values = new List<int>()"));
        Assert.That(scenario.Steps[1].StepText, Does.Contain("values?.Add(42)"));
        Assert.That(scenario.Steps[2].StepText, Does.Contain("Count: > 0"));
    }

    [Test]
    public async Task Without_generator_metadata_caller_text_and_explicit_arguments_still_work()
    {
        var value = 1;
        var reads = 0;
        var builder = When(() => { value = 42; }, callerFile: "no-catalog.cs", callerLine: 1)
            .Then(() => Accept(value), callerFile: "no-catalog.cs", callerLine: 2)
            .WithArgs(() => { reads++; return value; });
        Assert.That(reads, Is.Zero);
        var scenario = await builder.BDTestAsync();
        Assert.That(reads, Is.EqualTo(1));
        Assert.That(Required(scenario.Steps)[1].StepText, Is.EqualTo("Then () => Accept(value) 42"));
    }

    [Test]
    public async Task ValueTask_source_is_consumed_exactly_once()
    {
        var source = new SingleUseSource();
        var execution = When(() => source.Create()).Then(() => Accept(42)).BDTestAsync();
        Assert.That(execution.IsCompleted, Is.False);
        source.Complete();
        await execution.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.That(source.ResultsRead, Is.EqualTo(1));
    }

    [TestCase(false), TestCase(true)]
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
        await Assert.ThrowsAsync<TimeoutException>(async () => await builder.BDTestAsync());
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task Cancellation_aware_generated_metadata_omits_token_and_renders_other_arguments()
    {
        var scenario = await When(token => AcceptToken(42, token)).Then(() => Accept(42)).BDTestAsync();
        Assert.That(Required(scenario.Steps)[0].StepText, Is.EqualTo("When received 42"));
    }

    [Test]
    public void Failure_output_names_the_failing_statement_delegate()
    {
        var builder = When(() => { throw new InvalidOperationException("delegate failure"); }).Then(() => Accept(42));
        Assert.ThrowsAsync<InvalidOperationException>(async () => await builder.BDTestAsync());
        var step = Required(BDTestUtil.GetScenarios(TestRun).Single().Steps)[0];
        Assert.That(step.Output, Does.Contain("delegate failure").And.Contain("executing").And.Contain("When"));
    }

    private sealed class SkipDelegateStepAttribute : SkipStepAttribute { }

    [SkipDelegateStep, StepText("explicit generic step")]
    private static ValueTask<int> SkippedValueTask() => throw new InvalidOperationException("Skipped method must not run");

    private static void Accept(int value) { }

    [StepText("received {0}")]
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
