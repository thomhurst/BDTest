using System;
using System.Runtime.CompilerServices;

namespace BDTest.Test;

public abstract class AbstractContextBDTestBase<TContext> : BDTestBase where TContext : class, new()
{
    private readonly object _contextLock = new();
    private readonly ConditionalWeakTable<string, BDTestContext<TContext>> _contexts = new();

    public TContext Context => BDTestContext.TestContext;

    public BDTestContext<TContext> BDTestContext
    {
        get
        {
            lock (_contextLock)
            {
                _contexts.TryGetValue(BDTestExecutionId, out var bdTestContext);

                if (bdTestContext != null)
                {
                    return bdTestContext;
                }

                var testContext = Activator.CreateInstance<TContext>();
                bdTestContext = new BDTestContext<TContext>(this, testContext, BDTestExecutionId);

                ContextAmendment?.Invoke(bdTestContext);

                _contexts.Add(BDTestExecutionId, bdTestContext);
                return bdTestContext;
            }
        }
    }

    public void OverwriteContext(TContext testContext)
    {
        lock (_contextLock)
        {
            RemoveContext();
                
            var bdTestContext = new BDTestContext<TContext>(this, testContext, BDTestExecutionId);
                
            _contexts.Add(BDTestExecutionId, bdTestContext);
        }
    }

    protected void RemoveContext()
    {
        lock (_contextLock)
        {
            _contexts.Remove(BDTestExecutionId);
        }
    }
        
    public Action<BDTestContext<TContext>> ContextAmendment { get; set; }

    internal override void RecreateContextOnRetry()
    {
        OverwriteContext(Activator.CreateInstance<TContext>());   
        ContextAmendment?.Invoke(BDTestContext);
    }
}