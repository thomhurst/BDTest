using BDTest.Attributes;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[Parallelizable(ParallelScope.All)]
[Story(AsA = "BDTest developer",
    IWant = "to make sure that BDTest works asynchronously",
    SoThat = "tests run with maximum performance")]
public class AsyncTests : BDTestBase
{
    [TestCase("async")]
    [TestCase("await-builder")]
    public async Task StepsWaitForAsyncCompletion(string entryPoint)
    {
        var entered = CreateSignal();
        var release = CreateSignal();
        var events = new List<string>();
        var builder = Given(() => HoldStep(entered, release.Task, events))
            .When(() => RecordStep("when", events))
            .Then(() => RecordStep("then", events));

        var execution = entryPoint switch
        {
            "async" => builder.BDTestAsync(),
            _ => AwaitBuilder(builder)
        };

        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.That(execution.IsCompleted, Is.False);
            Assert.That(events, Is.EqualTo(new[] { "given started" }));
        }
        finally
        {
            release.TrySetResult(true);
            await execution.WaitAsync(TimeSpan.FromSeconds(20));
        }

        var scenario = await execution;
        Assert.That(events, Is.EqualTo(new[] { "given started", "given finished", "when", "then" }));
        Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
        Assert.That(Required(scenario.Steps).Select(step => step.Status), Is.All.EqualTo(Status.Passed));
        Assert.That(scenario.TimeTaken, Is.GreaterThanOrEqualTo(TimeSpan.Zero));
    }

    private static TaskCompletionSource<bool> CreateSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task HoldStep(TaskCompletionSource<bool> entered, Task release, List<string> events)
    {
        events.Add("given started");
        entered.SetResult(true);
        await release;
        events.Add("given finished");
    }

    private static async Task RecordStep(string name, List<string> events)
    {
        await Task.Yield();
        events.Add(name);
    }

    private static async Task<Scenario> AwaitBuilder(RunnableStepBuilder builder) => await builder;
}
