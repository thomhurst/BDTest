using BDTest.TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BDTest.MSTest.Tests;

[TestClass]
public class LifecycleTests
{
    [TestMethod]
    public Task ExplicitRunsKeepCompletionAndOutputIsolated() =>
        LifecycleContract.Verify(run => new Probe(run).Actions);

    private sealed class Probe(BDTestRun run) : MSTestBDTestBase<object>
    {
        public override BDTestRun TestRun => run;
        public LifecycleContract.Probe Actions => new(this, BeginTest, EndTest, () => Context);
    }
}
