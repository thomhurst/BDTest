namespace BDTest.Test;

public abstract class AbstractContextBDTestBase<TContext> : BDTestBase where TContext : class, new()
{
    private readonly object _contextLock = new();
    private readonly Dictionary<string, BDTestContext<TContext>> _contexts = new(StringComparer.Ordinal);

    public TContext Context => BDTestContext.TestContext;

    public BDTestContext<TContext> BDTestContext
    {
        get
        {
            lock (_contextLock)
            {
                var executionId = BDTestExecutionId;
                _contexts.TryGetValue(executionId, out var bdTestContext);

                if (bdTestContext != null)
                {
                    return bdTestContext;
                }

                var testContext = Activator.CreateInstance<TContext>();
                bdTestContext = new BDTestContext<TContext>(this, testContext, executionId);

                ContextAmendment?.Invoke(bdTestContext);

                _contexts.Add(executionId, bdTestContext);
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
                
            _contexts[executionId] = bdTestContext;
        }
    }

    protected void RemoveContext()
    {
        lock (_contextLock)
        {
            _contexts.Remove(BDTestExecutionId);
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