using BDTest.Attributes;
using BDTest.Test;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;
[Story(AsA = "BDTest developer",
    IWant = "to make sure that BDTest works asynchronously",
    SoThat = "tests run with maximum performance")]
public class AsyncTests : RunTestBase
{
    [Test]
    [Arguments("async")]
    [Arguments("await-builder")]
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
            await Assert.That(execution.IsCompleted).IsFalse();
            await Assert.That(events).IsEquivalentTo(new[] { "given started" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }
        finally
        {
            release.TrySetResult(true);
            await execution.WaitAsync(TimeSpan.FromSeconds(20));
        }

        var scenario = await execution;
        await Assert.That(events).IsEquivalentTo(new[] { "given started", "given finished", "when", "then" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(scenario.Status).IsEqualTo(Status.Passed);

        await Assert.That((Required(scenario.Steps).Select(step => step.Status)).All(item => Equals(item, Status.Passed))).IsTrue();
        await Assert.That(scenario.TimeTaken).IsGreaterThanOrEqualTo(TimeSpan.Zero);
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

    private static async Task<BDTest.Contracts.ScenarioResult> AwaitBuilder(RunnableStepBuilder builder) => await builder;
}
