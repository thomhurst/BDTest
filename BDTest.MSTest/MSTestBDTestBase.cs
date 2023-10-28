using BDTest.Test;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BDTest.MSTest;

[TestClass]
public abstract class MSTestBDTestBase<TContext> : AbstractContextBDTestBase<TContext>
    where TContext : class, new()
{
    [TestCleanup]
    public void TearDown()
    {
        RemoveContext();
        base.MarkTestAsComplete();
    }
}