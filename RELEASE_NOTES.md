# Release notes

## Next major release (unreleased)

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
