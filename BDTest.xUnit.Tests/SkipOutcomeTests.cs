using BDTest.Settings;
using BDTest.Test;
using Xunit;
using Xunit.Abstractions;

namespace BDTest.xUnit.Tests;

public class SkipOutcomeTests : XUnitBDTestBase<XUnitContext>
{
    static SkipOutcomeTests()
    {
        BDTestSettings.GlobalSkipTestRules.Add(s => s.BdTestBaseClass is SkipOutcomeTests);
    }

    public SkipOutcomeTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task RuntimeSkipReportsUnsupportedInsteadOfPassing()
    {
        var exception = await Assert.ThrowsAsync<NotSupportedException>(() =>
            When(() => MustNotRun()).Then(() => MustNotRun()).BDTestAsync());
        Assert.Contains("xUnit v2 does not support runtime skipping", exception.Message);
        var scenario = BDTestUtil.GetScenarios().Single(s => s.FrameworkTestId == BDTestExecutionId);
        Assert.Equal(Status.Skipped, scenario.Status);
        Assert.All(scenario.Steps, step => Assert.Equal(Status.Skipped, step.Status));
    }

    private static void MustNotRun() => throw new InvalidOperationException("Skipped step executed");
}
