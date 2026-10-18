---
title: Test context
description: Create, initialize and dispose per-test state with factories and dependency injection.
---

# Test context

A context holds the state of one test. All four framework adapters derive from `AbstractContextBDTestBase<TContext>` and expose `Context` and `BDTestContext`. Each execution has its own context, including parallel tests.

```csharp
public class AccountTests : TUnitBDTestBase<AccountContext>
{
    [Test]
    public async Task CreateAccount()
    {
        await When(() => CreateAccount(Context))
            .Then(() => TheResponseIsOk(Context))
            .BDTestAsync();
    }
}
```

By default, BDTest uses a public parameterless constructor. A custom factory allows constructor dependencies; context types no longer need a `new()` constraint.

## Factories

Override `CreateContextAsync` for a test base:

```csharp
protected override ValueTask<AccountContext> CreateContextAsync(ContextCreation creation)
{
    return new ValueTask<AccountContext>(new AccountContext(client));
}
```

`ContextCreation` exposes `ExecutionId`, zero-based `Attempt` (0 initially, 1 on the first retry), and `Services`. Each outline row starts at attempt 0.

`BDTestOptions.ContextFactory` and `Services` are nullable optional registrations. `ContextCreation.Services` is also nullable when no provider is configured. Check it in custom factories before resolving services. Factory methods return a non-null context; BDTest rejects a null result rather than exposing it through `Context`.

For a shared registration, implement `IBDTestContextFactory` in `BDTest.Test` and set `BDTestOptions.ContextFactory` before starting an [explicit run](../advanced/explicit-runs.md). Its method is `ValueTask<object> CreateContextAsync(Type contextType, ContextCreation creation)`. Return a new context assignable to `contextType`. The default protected factory delegates to this registration. Override `TestRun` on the adapter to select the run.

```csharp
var options = new BDTestOptions
{
    ContextFactory = new ServiceProviderContextFactory(),
    Services = services,
};
var run = BDTestRun.Start(options);
```

`ServiceProviderContextFactory` uses `ActivatorUtilities.CreateInstance` to create a new context with constructor dependencies from `Services`. BDTest owns the context. You own the service provider, its scope and resolved dependencies; BDTest never disposes them. Supply an appropriate scope yourself when using scoped services. The context itself is constructed directly, rather than resolved as a provider-owned service.

Factory and service-provider registrations are frozen when the run starts. Replacing either property on the original options does not change that run. Factory and provider objects are shared references and must support concurrent tests. The legacy default run has no process-global factory; override `CreateContextAsync` or use an explicit run.

## Configuring a new context

Override the synchronous `ConfigureContext` hook to set defaults after construction:

```csharp
protected override void ConfigureContext(BDTestContext<AccountContext> context)
{
    context.TestContext.Request = new HttpRequestMessage();
}
```

The wrapper exposes `TestContext`, story text, scenario text and the current scenario. The hook may read `Context` or `BDTestContext` without creating another context. `CurrentScenario` has type `Scenario?` and is null before a scenario exists, including during outline construction because the row does not yet exist. Check it before accessing scenario members. Do not call `OverwriteContext` or `OverwriteContextAsync` from this hook; BDTest rejects replacement with an `InvalidOperationException`. Configure the supplied object, or override `CreateContextAsync` to select another object. If construction or configuration fails, BDTest detaches and disposes any owned context that was created. A later access can create a fresh context.

## Asynchronous initialization

Implement `IBDTestContextLifetime` to initialize the context asynchronously:

```csharp
public sealed class AccountContext : IBDTestContextLifetime, IAsyncDisposable
{
    public async ValueTask InitializeAsync()
    {
        // Start resources, log in, or create a browser page here.
        await StartResourcesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await StopResourcesAsync();
    }
}
```

BDTest awaits initialization once before scenario hooks and the first step of each attempt. It also initializes fresh contexts before retry setup hooks and outline row factories. Initialization failures stop the attempt before any steps run, remove the stored context, and dispose it. Terminal construction, configuration and initialization errors are retained in the scenario exception for reporting, even when no step ran, including preparation that throws `NotImplementedException`. A successful retry keeps earlier errors only in its attempt history. A failure during cleanup is propagated; if initialization or configuration also failed, both exceptions are retained in an `AggregateException`.

Setup-time synchronous `Context` access remains supported: it constructs and configures the context through a worker-thread bridge, which also supports async factories without blocking their synchronization context. It does **not** run `InitializeAsync`. Prefer `await InitializeContextAsync()` in async setup if setup needs initialized resources or an asynchronous factory. Subsequent synchronous access returns the configured context. Do not capture a context or a context-dependent step object across retries; read `Context` inside each step.

## Ownership and disposal

BDTest disposes each framework-owned context exactly once:

- before constructing a replacement on retry or for the next outline row;
- when an owned context is replaced with `OverwriteContext`;
- when preparation fails after a context has been constructed;
- at adapter teardown, including when steps throw.

`IAsyncDisposable` takes precedence over `IDisposable` when both are implemented. Normal adapter teardown awaits async disposal. Synchronous compatibility entry points bridge async disposal; custom adapters should await `EndTestAsync()` in teardown. A disposal exception propagates to the runner, but the context is detached and execution bookkeeping is still completed. A failure disposing a retry context stops that retry before another context is created.

Cleanup must use the context object being disposed. Reading the fixture's `Context` or `BDTestContext`, initializing, replacing or removing that context from its cleanup callback throws `InvalidOperationException` immediately. This prevents cleanup from waiting on its own execution gate. Concurrent initialization from another flow still waits until cleanup completes.

`OverwriteContext(context)` and `await OverwriteContextAsync(context)` install a **caller-owned** context. BDTest still initializes it before steps but does not dispose it. Pass `takeOwnership: true` to transfer ownership. Replacement first disposes the previous framework-owned context. During retry disposal, concurrent initialization waits for the replacement and uses the retry attempt number. The replacement is not installed if that cleanup fails. Reinstalling the same object does not dispose it; ownership follows the flag supplied on the latest call. Configuration hooks run for factory-created contexts, not supplied replacements.

## Migration

`ContextAmendment` is replaced by the protected `ConfigureContext(BDTestContext<TContext>)` override. `Inject<T>()` is removed; [construct step classes explicitly](step-classes.md) or resolve helpers with your configured service provider.

**Disposal is a behavior change.** Remove manual disposal of framework-owned contexts from your teardown to avoid double disposal. Keep manually disposing caller-owned replacements and provider-owned dependencies.

## Inline contexts

`BDTestBase.WithContext<T>(...)` is the older parameterless-construction helper. It passes an object directly to your delegate and does not use adapter context storage, factories, initialization, retry recreation or ownership. Manage that object's lifetime yourself, or use a context base class for the managed lifetime above.

## Parallel tests

Keep state in the context rather than static or fixture fields. Expose step classes through getter properties that bind to the current `Context`. Dispose explicit runs only after all their tests and teardown have finished.
