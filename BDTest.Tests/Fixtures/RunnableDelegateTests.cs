using System.Collections.Concurrent;
using BDTest.Attributes;
using BDTest.Settings;
using BDTest.Test;
using BDTest.Test.Steps;
using BDTest.Test.Steps.When;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
public class RunnableDelegateTests
{
    [TestCase("Action")]
    [TestCase("Task")]
    [TestCase("CancellableTask")]
    public async Task Repeated_execution_reuses_the_delegate_and_reads_current_state(string kind)
    {
        using var probe = new Probe();
        var runnable = probe.CreateRunnable(kind);
        var original = runnable.StepDelegate;
        Assert.That(probe.Values, Is.Empty);

        using var firstCancellation = new CancellationTokenSource();
        using var secondCancellation = new CancellationTokenSource();
        probe.Value = 10;
        await runnable.Run(firstCancellation.Token);
        probe.Value = 20;
        await runnable.Run(secondCancellation.Token);

        Assert.That(runnable.StepDelegate, Is.SameAs(original));
        Assert.That(probe.Values, Is.EqualTo(new[] { 10, 20 }));
        if (kind == "CancellableTask")
            Assert.That(probe.Tokens, Is.EqualTo(new[] { firstCancellation.Token, secondCancellation.Token }));
    }

    [TestCase("Action")]
    [TestCase("Task")]
    [TestCase("CancellableTask")]
    public async Task A_step_failure_does_not_replace_the_delegate(string kind)
    {
        using var probe = new Probe { FailuresRemaining = 1 };
        var runnable = probe.CreateRunnable(kind);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => runnable.Run());
        Assert.That(Required(error).Message, Is.EqualTo("step failure"));

        await runnable.Run();

        Assert.That(probe.Values, Has.Count.EqualTo(2));
    }

    [TestCase("Action")]
    [TestCase("Task")]
    [TestCase("CancellableTask")]
    public async Task A_scenario_retry_reuses_the_delegate_and_reads_recreated_context(string kind)
    {
        using var probe = new Probe { FailuresRemaining = 1 };
        var builder = probe.CreateScenario(kind).Then(() => Console.WriteLine("done"));
        var original = builder.ExistingSteps[0].Runnable;
        var scenario = await builder.BDTestAsync();
        Assert.That(builder.ExistingSteps[0].Runnable, Is.SameAs(original));

        Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
        Assert.That(scenario.RetryCount, Is.EqualTo(1));
        Assert.That(probe.Contexts, Has.Count.EqualTo(2));
        Assert.That(probe.Contexts[0], Is.Not.SameAs(probe.Contexts[1]));
        Assert.That(probe.Contexts.Select(context => context.Calls), Is.EqualTo(new[] { 1, 1 }));
    }

    [TestCase("Action")]
    [TestCase("Task")]
    [TestCase("CancellableTask")]
    public async Task Skipped_steps_never_invoke_the_delegate(string kind)
    {
        using var probe = new Probe();
        var step = new Step(probe.CreateRunnable(kind), StepType.When, "scenario", "report");
        var previous = BDTestSettings.Debug.ShouldSkipWhenStep;
        try
        {
            BDTestSettings.Debug.ShouldSkipWhenStep = true;
            await step.Execute(null);

            Assert.That(step.Status, Is.EqualTo(Status.Skipped));
            Assert.That(probe.Values, Is.Empty);
        }
        finally
        {
            BDTestSettings.Debug.ShouldSkipWhenStep = previous;
        }
    }

    [Test]
    public async Task Concurrent_execution_reuses_a_shared_delegate()
    {
        var calls = new ConcurrentBag<int>();
        var runnable = new Runnable(() => calls.Add(1));
        var original = runnable.StepDelegate;
        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => runnable.Run())));
        Assert.That(calls, Has.Count.EqualTo(16));
        Assert.That(runnable.StepDelegate, Is.SameAs(original));
    }

    public sealed class ProbeContext
    {
        public int Calls { get; set; }
    }

    [BDTestRetry(1)]
    private sealed class Probe : AbstractContextBDTestBase<ProbeContext>, IDisposable
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

        public void Dispose()
        {
            RemoveContext();
            BDTest.Tests.Helpers.LegacyLifecycle.Complete(this);
        }
    }
}
