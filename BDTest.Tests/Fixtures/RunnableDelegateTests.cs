using System.Collections.Concurrent;
using BDTest;
using BDTest.Settings;
using BDTest.Test;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[NotInParallel]
public class RunnableDelegateTests : TestRunOwner
{
    [Test]
    [Arguments("Action")]
    [Arguments("Task")]
    [Arguments("CancellableTask")]
    public async Task Repeated_execution_reuses_the_delegate_and_reads_current_state(string kind)
    {
        using var probe = new Probe();
        var runnable = probe.CreateRunnable(kind);
        var original = runnable.StepDelegate;
        await Assert.That(probe.Values).IsEmpty();

        using var firstCancellation = new CancellationTokenSource();
        using var secondCancellation = new CancellationTokenSource();
        probe.Value = 10;
        await runnable.Run(firstCancellation.Token);
        probe.Value = 20;
        await runnable.Run(secondCancellation.Token);

        await Assert.That(runnable.StepDelegate).IsSameReferenceAs(original);
        await Assert.That(probe.Values).IsEquivalentTo(new[] { 10, 20 }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        if (kind == "CancellableTask")
            await Assert.That(probe.Tokens).IsEquivalentTo(new[] { firstCancellation.Token, secondCancellation.Token }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    [Arguments("Action")]
    [Arguments("Task")]
    [Arguments("CancellableTask")]
    public async Task A_step_failure_does_not_replace_the_delegate(string kind)
    {
        using var probe = new Probe { FailuresRemaining = 1 };
        var runnable = probe.CreateRunnable(kind);
        var error = await Assert.That(() => runnable.Run()).ThrowsExactly<InvalidOperationException>();
        await Assert.That(Required(error).Message).IsEqualTo("step failure");

        await runnable.Run();

        await Assert.That(probe.Values).Count().IsEqualTo(2);
    }

    [Test]
    [Arguments("Action")]
    [Arguments("Task")]
    [Arguments("CancellableTask")]
    public async Task A_scenario_retry_reuses_the_delegate_and_reads_recreated_context(string kind)
    {
        using var probe = new Probe { FailuresRemaining = 1 };
        var builder = probe.CreateScenario(kind).Then(() => Console.WriteLine("done"));
        var original = builder.ExistingSteps[0].Runnable;
        var scenario = await builder.BDTestAsync();
        await Assert.That(builder.ExistingSteps[0].Runnable).IsSameReferenceAs(original);

        await Assert.That(scenario.Status).IsEqualTo(Status.Passed);
        await Assert.That(scenario.RetryCount).IsEqualTo(1);
        await Assert.That(probe.Contexts).Count().IsEqualTo(2);
        await Assert.That(probe.Contexts[0]).IsNotSameReferenceAs(probe.Contexts[1]);
        await Assert.That(probe.Contexts.Select(context => context.Calls)).IsEquivalentTo(new[] { 1, 1 }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    [Arguments("Action")]
    [Arguments("Task")]
    [Arguments("CancellableTask")]
    public async Task Skipped_steps_never_invoke_the_delegate(string kind)
    {
        using var probe = new Probe();
        Options.Debug.ShouldSkipWhenStep = true;
        var step = new Step(probe.CreateRunnable(kind), StepType.When, "scenario", "report", TestRun);
        Options.Debug.ShouldSkipWhenStep = true;
        await step.Execute(null);

        await Assert.That(step.Status).IsEqualTo(Status.Skipped);
        await Assert.That(probe.Values).IsEmpty();

    }

    [Test]
    public async Task Concurrent_execution_reuses_a_shared_delegate()
    {
        var calls = new ConcurrentBag<int>();
        var runnable = new Runnable(() => calls.Add(1));
        var original = runnable.StepDelegate;
        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => runnable.Run())));
        await Assert.That(calls).Count().IsEqualTo(16);
        await Assert.That(runnable.StepDelegate).IsSameReferenceAs(original);
    }

    public sealed class ProbeContext
    {
        public int Calls { get; set; }
    }

    [BDTestRetry(1)]
    private sealed class Probe : RunContextTestBase<ProbeContext>, IDisposable
    {
        private readonly string _executionId = Guid.NewGuid().ToString();
        protected override string BDTestExecutionId => _executionId;
        public int Value { get; set; }
        public int FailuresRemaining { get; set; }
        public List<int> Values { get; } = new();
        public List<CancellationToken> Tokens { get; } = new();
        public List<ProbeContext> Contexts { get; } = new();
        public Runnable CreateRunnable(string kind) => kind switch
        {
            "Action" => new Runnable(Record),
            "Task" => new Runnable((Func<Task>)RecordAsync),
            "CancellableTask" => new Runnable((Func<CancellationToken, Task>)RecordAsync),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        public When CreateScenario(string kind) => kind switch
        {
            "Action" => When(Record),
            "Task" => When((Func<Task>)RecordAsync),
            "CancellableTask" => When((Func<CancellationToken, Task>)RecordAsync),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        private void Record()
        {
            Values.Add(Value);
            Contexts.Add(Context);
            Context.Calls++;
            if (FailuresRemaining-- > 0)
                throw new InvalidOperationException("step failure");
        }

        private async Task RecordAsync()
        {
            await Task.Yield();
            Record();
        }

        private async Task RecordAsync(CancellationToken token)
        {
            await Task.Yield();
            Tokens.Add(token);
            Record();
        }

        public override void Dispose()
        {
            RemoveContext();
            base.Dispose();
        }
    }
}
