using System.Runtime.CompilerServices;
using System.Threading;

namespace BDTest.Test;

public abstract class AbstractContextBDTestBase<TContext> : BDTestBase where TContext : class
{
    private readonly object _contextLock = new();
    private readonly Dictionary<string, ContextEntry> _contexts = new(StringComparer.Ordinal);
    private readonly ConditionalWeakTable<string, ContextEntry> _defaultContexts = new();
    private readonly AsyncLocal<ContextEntry> _configuring = new();

    private sealed class ContextEntry
    {
        internal readonly SemaphoreSlim Gate = new(1, 1);
        internal int Attempt;
        internal BDTestContext<TContext> Context;
        internal bool Owned = true;
        internal bool Initialized;
        internal bool Retired;
        internal volatile bool Configured;
    }

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
            var executionId = BDTestExecutionId;
            var entry = GetEntry(executionId);
            // ConfigureContext may read the object currently being configured.
            var context = entry.Context;
            if (context != null && (entry.Configured || ReferenceEquals(_configuring.Value, entry)))
                return context;
            return ContextAsyncBridge.Run(() => PrepareContextAsync(executionId, entry, initialize: false));
        }
    }

    /// <summary>Creates and initializes the context, including when called from asynchronous setup.</summary>
    public ValueTask InitializeContextAsync() => InitializeExecutionContextAsync();

    internal override ValueTask InitializeExecutionContextAsync()
    {
        var executionId = BDTestExecutionId;
        return InitializeContextForExecutionAsync(executionId);
    }

    private async ValueTask InitializeContextForExecutionAsync(string executionId)
    {
        await PrepareContextAsync(executionId, GetEntry(executionId), initialize: true).ConfigureAwait(false);
    }

    /// <summary>Creates a framework-owned context using the owning run's frozen registrations.</summary>
    protected virtual async ValueTask<TContext> CreateContextAsync(ContextCreation creation)
    {
        var factory = TestRun.Options.ContextFactory;
        if (factory == null) return Activator.CreateInstance<TContext>();
        return (TContext)await factory.CreateContextAsync(typeof(TContext), creation).ConfigureAwait(false);
    }

    /// <summary>Configures a newly constructed context before asynchronous initialization.</summary>
    protected virtual void ConfigureContext(BDTestContext<TContext> context) { }

    private ContextEntry GetEntry(string executionId)
    {
        lock (_contextLock)
        {
            if (IsDefaultExecutionId(executionId))
                return _defaultContexts.GetValue(executionId, _ => new ContextEntry());
            if (!_contexts.TryGetValue(executionId, out var entry))
                _contexts.Add(executionId, entry = new ContextEntry());
            return entry;
        }
    }

    private async ValueTask<BDTestContext<TContext>> PrepareContextAsync(string executionId, ContextEntry entry, bool initialize)
    {
        await entry.Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (entry.Retired) throw new InvalidOperationException("This context execution has completed.");
            return await PrepareContextUnderGateAsync(executionId, entry, initialize).ConfigureAwait(false);
        }
        finally { entry.Gate.Release(); }
    }

    // The execution keeps one gate across disposal, retry replacement and concurrent access.
    private async ValueTask<BDTestContext<TContext>> PrepareContextUnderGateAsync(string executionId, ContextEntry entry, bool initialize)
    {
        try
        {
            if (entry.Context == null)
            {
                var context = await CreateContextAsync(new ContextCreation(executionId, entry.Attempt, TestRun.Options.Services)).ConfigureAwait(false);
                if (context == null) throw new InvalidOperationException("The context factory returned null.");
                entry.Context = new BDTestContext<TContext>(this, context, executionId);
                entry.Owned = true;
                var previous = _configuring.Value;
                try
                {
                    _configuring.Value = entry;
                    ConfigureContext(entry.Context);
                    entry.Configured = true;
                }
                finally { _configuring.Value = previous; }
            }
            if (initialize && !entry.Initialized)
            {
                if (entry.Context.TestContext is IBDTestContextLifetime lifetime)
                    await lifetime.InitializeAsync().ConfigureAwait(false);
                entry.Initialized = true;
            }
            return entry.Context;
        }
        catch (Exception error)
        {
            try { await DisposeContextAsync(entry).ConfigureAwait(false); }
            catch (Exception cleanupError) { throw new AggregateException(error, cleanupError); }
            throw;
        }
    }

    /// <summary>Replaces the current context. The caller retains ownership unless explicitly transferred.</summary>
    public void OverwriteContext(TContext testContext, bool takeOwnership = false)
    {
        var executionId = BDTestExecutionId;
        ContextAsyncBridge.Run(() => OverwriteContextForExecutionAsync(executionId, testContext, takeOwnership));
    }

    /// <summary>Disposes the previous owned context before installing the replacement.</summary>
    public ValueTask OverwriteContextAsync(TContext testContext, bool takeOwnership = false)
    {
        var executionId = BDTestExecutionId;
        return OverwriteContextForExecutionAsync(executionId, testContext, takeOwnership);
    }

    private async ValueTask OverwriteContextForExecutionAsync(string executionId, TContext testContext, bool takeOwnership)
    {
        if (testContext == null) throw new ArgumentNullException(nameof(testContext));
        var entry = GetEntry(executionId);
        if (ReferenceEquals(_configuring.Value, entry))
            throw new InvalidOperationException("Cannot replace a context from ConfigureContext. Configure the supplied context or override CreateContextAsync instead.");
        await entry.Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (entry.Retired) throw new InvalidOperationException("This context execution has completed.");
            if (!ReferenceEquals(entry.Context?.TestContext, testContext))
            {
                await DisposeContextAsync(entry).ConfigureAwait(false);
                entry.Context = new BDTestContext<TContext>(this, testContext, executionId);
                entry.Initialized = false;
            }
            entry.Owned = takeOwnership;
            entry.Configured = true;
        }
        finally { entry.Gate.Release(); }
    }

    protected void RemoveContext()
    {
        var executionId = BDTestExecutionId;
        ContextAsyncBridge.Run(() => RemoveExecutionContextAsync(executionId));
    }
    protected ValueTask RemoveContextAsync() => RemoveExecutionContextAsync(BDTestExecutionId);

    internal override async ValueTask RemoveExecutionContextAsync(string executionId)
    {
        ContextEntry entry;
        lock (_contextLock)
        {
            if (IsDefaultExecutionId(executionId)) _defaultContexts.TryGetValue(executionId, out entry);
            else _contexts.TryGetValue(executionId, out entry);
            if (entry == null) return;
        }
        await entry.Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            entry.Retired = true;
            await DisposeContextAsync(entry).ConfigureAwait(false);
        }
        finally
        {
            ForgetEntry(executionId, entry);
            entry.Gate.Release();
        }
    }

    private void ForgetEntry(string executionId, ContextEntry entry)
    {
        lock (_contextLock)
        {
            if (_defaultContexts.TryGetValue(executionId, out var defaultEntry) && ReferenceEquals(defaultEntry, entry))
                _defaultContexts.Remove(executionId);
            if (_contexts.TryGetValue(executionId, out var stored) && ReferenceEquals(stored, entry))
                _contexts.Remove(executionId);
        }
    }

    private static async ValueTask DisposeContextAsync(ContextEntry entry)
    {
        var context = entry.Context?.TestContext;
        entry.Configured = false;
        entry.Initialized = false;
        entry.Context = null;
        if (!entry.Owned) return;
        // Async disposal takes precedence when both interfaces are implemented.
        if (context is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        else if (context is IDisposable disposable)
            disposable.Dispose();
    }

    internal override async ValueTask RecreateContextOnRetryAsync(int attempt)
    {
        var executionId = BDTestExecutionId;
        var entry = GetEntry(executionId);
        await entry.Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (entry.Retired) throw new InvalidOperationException("This context execution has completed.");
            await DisposeContextAsync(entry).ConfigureAwait(false);
            entry.Attempt = attempt;
            await PrepareContextUnderGateAsync(executionId, entry, initialize: true).ConfigureAwait(false);
        }
        finally { entry.Gate.Release(); }
    }
}
