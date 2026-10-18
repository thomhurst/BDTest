using System.Threading;
using BDTest.Test;

namespace BDTest.TUnit.Tests;

public class TUnitExecutionIdTests
{
    [Test]
    public async Task CurrentTestContextUsesRunnerId()
    {
        var fixture = new AdapterProbe();
        try
        {
            var context = fixture.Context;
            await Task.Yield();

            await Assert.That(fixture.ExecutionId).IsEqualTo(TestContext.Current!.Id);
            await Assert.That(fixture.BDTestContext.CurrentScenarioBDTestExecutionId).IsEqualTo(TestContext.Current!.Id);
            await Assert.That(ReferenceEquals(fixture.Context, context)).IsTrue();
        }
        finally
        {
            fixture.TearDown();
        }
    }

    [Test]
    public async Task ExecutionWithoutFlowingTestContextUsesStableFallback()
    {
        Task<Scenario> execution;
        using (ExecutionContext.SuppressFlow())
        {
            execution = Task.Run(() => RunWithoutCurrentTestAsync("background startup"));
        }

        var scenario = await execution;

        await Assert.That(scenario.Status).IsEqualTo(Status.Passed);
        await Assert.That(scenario.TestStartupInformation).Contains("background startup");
        await Assert.That(scenario.FrameworkTestId).IsNotEqualTo(TestContext.Current!.Id);
        await Assert.That(new AdapterProbe().ExecutionId).IsEqualTo(TestContext.Current!.Id);
    }

    private static async Task<Scenario> RunWithoutCurrentTestAsync(string startupOutput)
    {
        await Assert.That(TestContext.Current).IsNull();
        var fixture = new AdapterProbe();
        try
        {
            var id = fixture.ExecutionId;
            var context = fixture.Context;
            fixture.WriteStartupOutput(startupOutput);
            await Task.Yield();

            await Assert.That(string.IsNullOrEmpty(id)).IsFalse();
            await Assert.That(fixture.ExecutionId).IsEqualTo(id);
            await Assert.That(ReferenceEquals(fixture.Context, context)).IsTrue();
            await Assert.That(fixture.BDTestContext.CurrentScenarioBDTestExecutionId).IsEqualTo(id);

            var scenario = await fixture.When(() => NoOp()).Then(() => NoOp()).BDTestAsync();
            await Assert.That(scenario.FrameworkTestId).IsEqualTo(id);
            return scenario;
        }
        finally
        {
            fixture.TearDown();
        }
    }

    private static void NoOp() { }

    private sealed class AdapterProbe : TUnitBDTestBase<TUnitContext>
    {
        public string ExecutionId => BDTestExecutionId;
    }
}
