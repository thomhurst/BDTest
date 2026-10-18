---
title: xUnit v3
---

`BDTest.xUnit` requires xUnit v3 in the next major BDTest release. The adapter
remains signed and targets `netstandard2.0`. Replace `xunit` 2.x with
`xunit.v3.mtp-off` to use VSTest, keep `Microsoft.NET.Test.Sdk` and
`xunit.runner.visualstudio`, and set `<OutputType>Exe</OutputType>` in your test
project. The repository uses framework package version `4.0.2` and runner 4.x.

Inherit from `XUnitBDTestBase<TContext>` without passing an output helper to the
base constructor. For output in derived tests, inject `Xunit.ITestOutputHelper`
instead of `Xunit.Abstractions.ITestOutputHelper`. Execution IDs use the public
`TestContext.Current.Test.UniqueID`; deferred theory rows receive separate IDs
and contexts. Runtime BDTest skips are reported as skipped xUnit results.

## Asynchronous teardown

xUnit v3 calls `DisposeAsync()` instead of `Dispose()` for tests that implement
`IAsyncDisposable` or `IAsyncLifetime`. The adapter implements `IAsyncDisposable`
and provides a virtual `DisposeAsync()` that completes the BDTest execution,
removes its context, and clears pending output and listener state.

Override this method for asynchronous teardown. Always await the base method in
a `finally` block so BDTest cleanup also runs when your teardown fails:

```csharp
public override async ValueTask DisposeAsync()
{
    try
    {
        await Context.CloseAsync();
    }
    finally
    {
        await base.DisposeAsync();
    }
}
```

This also applies when implementing `IAsyncLifetime.InitializeAsync()`. Change
an existing `DisposeAsync()` implementation to an override. If you hide the
method or explicitly reimplement `IAsyncDisposable.DisposeAsync()`, you must
still call `await base.DisposeAsync()`; xUnit does not also call `Dispose()`.

See the [xUnit v3 migration guide](https://xunit.net/docs/getting-started/v3/migration#iasynclifetime-now-inherits-from-iasyncdisposable)
for the runner's disposal behavior.
