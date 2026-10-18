# Release notes

## Next major release (unreleased)

### Breaking: managed context lifetime and factories

Context bases and all four adapters now support constructor dependencies through
`CreateContextAsync(ContextCreation)` or a run's frozen `BDTestOptions.ContextFactory`
and `Services`. `ServiceProviderContextFactory` constructs a new context with
`ActivatorUtilities`; callers retain ownership of the provider and its dependencies.
The `new()` constraint is removed. The default factory retains parameterless construction.

BDTest awaits `IBDTestContextLifetime.InitializeAsync()` before scenario steps,
retry setup and outline row factories. It disposes framework-owned contexts before
replacement and at teardown, including when a step or initialization fails.
`IAsyncDisposable` takes precedence over `IDisposable`. **Remove manual disposal
of framework-owned contexts to avoid double disposal.** `OverwriteContext` values
remain caller-owned unless `takeOwnership: true` is supplied.

Replace `ContextAmendment` with a protected `ConfigureContext(BDTestContext<TContext>)`
override. Replace `Inject<T>()` with explicit construction or service resolution.
Synchronous setup access still constructs/configures the context; await
`InitializeContextAsync()` when setup needs initialized resources. Custom adapters
should await `EndTestAsync()` during teardown. Legacy synchronous completion hooks
remain supported through a compatibility bridge. Inline `WithContext` delegates
retain their caller-managed lifetime.

See [test context](https://thomhurst.github.io/BDTest/docs/writing-tests/test-context)
for ownership and migration details. Tracked by [#824](https://github.com/thomhurst/BDTest/issues/824).

### Breaking: asynchronous scenario execution only

`RunnableStepBuilder.BDTest()` and `ScenarioOutlineBuilder<TExample>.BDTest()` have
been removed. Both builders still support `BDTestAsync()` and direct `await`, and
both entry points retain the single-execution guard, including after failure and
while execution is in progress. Build a new chain or outline for another execution.

Change each test method that calls `.BDTest()` from `void` to `async Task` and
await `.BDTestAsync()`, or directly await the builder. For example:

```csharp
// Before
[Test]
public void Login() => Given(() => User()).When(() => LogIn()).Then(() => LoggedIn()).BDTest();

// After
[Test]
public async Task Login() => await Given(() => User()).When(() => LogIn()).Then(() => LoggedIn());
```

Use the same pattern for scenario outlines and await asynchronous test helpers and
`WithContext<TContext>(...)` calls. NUnit, xUnit, MSTest, and TUnit support `Task`
test methods. Step methods may still be synchronous.

Scenario execution starts inline and no longer has a blocking thread-pool mode.
Steps, hooks, retries, and outline rows preserve the caller's synchronization
context. A step may opt out within its own code with `ConfigureAwait(false)`;
subsequent steps still start on the caller's context. Timeout-enabled step bodies
continue to run on the thread pool so timeouts can bound synchronous work.

Do not replace `.BDTest()` with `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()`;
blocking can deadlock a single-threaded synchronization context. Tests that relied
on the old thread-pool scheduling must schedule that work explicitly. Use the
fixture's `Context` to share state between steps, rather than mutations to `AsyncLocal` values.

See the [migration guide](https://thomhurst.github.io/BDTest/docs/migration/async-only)
for outlines and context helpers. This breaking change is tracked by
[#801](https://github.com/thomhurst/BDTest/issues/801). No analyzer or code fix is included.
