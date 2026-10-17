using System.Runtime.CompilerServices;

namespace BDTest.Test;

public abstract class AbstractContextBDTestBase<TContext> : BDTestBase where TContext : class, new()
{
    private readonly object _contextLock = new();
    private readonly Dictionary<string, BDTestContext<TContext>> _contexts = new(StringComparer.Ordinal);

    // Default IDs can originate in child flows whose IDs never reach teardown.
    // Their stable string identity lets the context follow the execution flow's lifetime.
    private readonly ConditionalWeakTable<string, BDTestContext<TContext>> _defaultContexts = new();

    public TContext Context => BDTestContext.TestContext;

    public BDTestContext<TContext> BDTestContext
    {
        get
        {
            lock (_contextLock)
            {
                var executionId = BDTestExecutionId;
                BDTestContext<TContext> bdTestContext;
                if (IsDefaultExecutionId(executionId))
                {
                    _defaultContexts.TryGetValue(executionId, out bdTestContext);
                }
                else
                {
                    _contexts.TryGetValue(executionId, out bdTestContext);
                }

                if (bdTestContext != null)
                {
                    return bdTestContext;
                }

                var testContext = Activator.CreateInstance<TContext>();
                bdTestContext = new BDTestContext<TContext>(this, testContext, executionId);

                ContextAmendment?.Invoke(bdTestContext);

                StoreContext(executionId, bdTestContext);
                return bdTestContext;
            }
        }
    }

    public void OverwriteContext(TContext testContext)
    {
        lock (_contextLock)
        {
            var executionId = BDTestExecutionId;
                
            var bdTestContext = new BDTestContext<TContext>(this, testContext, executionId);
                
            StoreContext(executionId, bdTestContext);
        }
    }

    // Called only while holding _contextLock.
    private void StoreContext(string executionId, BDTestContext<TContext> context)
    {
        if (IsDefaultExecutionId(executionId))
        {
            _defaultContexts.Remove(executionId);
            _defaultContexts.Add(executionId, context);
        }
        else
        {
            _contexts[executionId] = context;
        }
    }

    protected override void MarkTestAsComplete()
    {
        RemoveContext();
        base.MarkTestAsComplete();
    }

    protected void RemoveContext()
    {
        lock (_contextLock)
        {
            var executionId = BDTestExecutionId;
            _defaultContexts.Remove(executionId);
            _contexts.Remove(executionId);
        }
    }

    protected T Inject<T>()
    {
        if (typeof(T).GetConstructors().Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(TContext))))
        {
            return (T)Activator.CreateInstance(typeof(T), Context);
        }
        
        return (T)Activator.CreateInstance(typeof(T));
    }
        
    public Action<BDTestContext<TContext>> ContextAmendment { get; set; }

    internal override void RecreateContextOnRetry()
    {
        OverwriteContext(Activator.CreateInstance<TContext>());   
        ContextAmendment?.Invoke(BDTestContext);
    }
}