using System;
using BDTest.Test;

namespace BDTest.xUnit
{
    public abstract class XUnitBDTestBase<TContext> : AbstractContextBDTestBase<TContext>, IDisposable
        where TContext : class, new()
    {
        public void Dispose()
        {
            RemoveContext();
            base.MarkTestAsComplete();
        }
    }
}
