using BDTest.Test;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BDTest.MSTest;

[TestClass]
public abstract class MSTestBDTestBase<TContext> : AbstractContextBDTestBase<TContext>
    where TContext : class, new()
{
    // MSTest creates a fixture instance per test, including each data-driven case.
    // An instance ID survives the separate execution flows used by async setup/body.
    protected override string BDTestExecutionId { get; } = Guid.NewGuid().ToString("N");

    [TestInitialize]
    public void InitializeBDTest()
    {
        MarkTestAsStarted();
    }

    [TestCleanup]
    public void TearDown()
    {
        RemoveContext();
        base.MarkTestAsComplete();
    }
}