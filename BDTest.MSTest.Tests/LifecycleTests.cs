using BDTest.TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BDTest.MSTest.Tests;

[TestClass]
public class LifecycleTests
{
    [TestMethod]
    public Task ExplicitRunsKeepCompletionAndOutputIsolated() =>
        LifecycleContract.Verify(run => new Probe(run).Actions);

    [TestMethod]
    public Task LegacyIdsKeepCaptureAndCleanupTogether() =>
        LifecycleContract.VerifyLegacy(run => new LegacyProbe(run).Actions);

    private sealed class LegacyProbe(BDTestRun run) : MSTestBDTestBase<object>
    {
        public override BDTestRun TestRun => run;
        protected override string BDTestExecutionId => "legacy-id";
        public LifecycleContract.Probe Actions => new(this, _ => InitializeBDTest(), () => TearDown(), () => Context);
    }

    private sealed class Probe(BDTestRun run) : MSTestBDTestBase<object>
    {
        public override BDTestRun TestRun => run;
        public LifecycleContract.Probe Actions => new(this, BeginTest, EndTest, () => Context);
    }
}
