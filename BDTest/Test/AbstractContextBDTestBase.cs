using System.Runtime.CompilerServices;
using System.Threading;

namespace BDTest.Test;

public abstract class AbstractContextBDTestBase<TContext> : BDTestBase where TContext : class, new()
{
    private readonly object _contextLock = new();
    private readonly Dictionary<string, BDTestContext<TContext>> _contexts = new(StringComparer.Ordinal);

    // Default IDs can originate in child flows whose IDs never reach teardown.
    // Their stable string identity lets the context follow the execution flow's lifetime.
    private readonly ConditionalWeakTable<string, BDTestContext<TContext>> _defaultContexts = new();

    public TContext Context => BDTestContext.TestContext;

    /// <summary>Runs before each scenario attempt with that attempt's typed context.</summary>
    public virtual Task OnBeforeScenario(ScenarioHookContext<TContext> scenario) => Task.CompletedTask;

    /// <summary>Runs after each scenario attempt with its final status and typed context.</summary>
    public virtual Task OnAfterScenario(ScenarioHookContext<TContext> scenario) => Task.CompletedTask;

    public override Task OnBeforeScenario(ScenarioHookContext scenario) =>
        scenario is ScenarioHookContext<TContext> typed ? OnBeforeScenario(typed) : base.OnBeforeScenario(scenario);

    public override Task OnAfterScenario(ScenarioHookContext scenario) =>
        scenario is ScenarioHookContext<TContext> typed ? OnAfterScenario(typed) : base.OnAfterScenario(scenario);

    internal override ScenarioHookContext CreateScenarioHookContext(Scenario scenario, CancellationToken token) =>
        new ScenarioHookContext<TContext>(scenario, Context, token);

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

                return CreateContext(executionId);
            }
        }
    }

    // Called only while holding _contextLock, including during retry recreation.
    private BDTestContext<TContext> CreateContext(string executionId)
    {
        var context = new BDTestContext<TContext>(this, Activator.CreateInstance<TContext>(), executionId);

        // Amendments may read Context or BDTestContext through the reentrant lock.
        StoreContext(executionId, context);
        try
        {
            ContextAmendment?.Invoke(context);
            return context;
        }
        catch
        {
            // Never reuse a partially initialized context after an amendment fails.
            _defaultContexts.Remove(executionId);
            _contexts.Remove(executionId);
            throw;
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

    protected void RemoveContext() => RemoveExecutionContext(BDTestExecutionId);

    internal override void RemoveExecutionContext(string executionId)
    {
        lock (_contextLock)
        {
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
        lock (_contextLock)
        {
            CreateContext(BDTestExecutionId);
        }
    }
}
