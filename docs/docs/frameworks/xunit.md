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

## Initialization and constructor output

The adapter implements `IAsyncLifetime`. Its explicit initialization hook calls
`BeginTest` after the derived constructor finishes, then invokes virtual
`InitializeAsync()`. Console output from constructors is buffered until that
point, so a `TestRun` assigned in the derived constructor owns the output.

For async setup, override `InitializeAsync()` with a `ValueTask` return type.
Prefer the virtual override to reimplementing `IAsyncLifetime`. The override does not need
to call the base method; capture is already active when it starts.

```csharp
public override async ValueTask InitializeAsync()
{
    await Context.OpenAsync();
}
```

Existing fixtures that reimplement `IAsyncLifetime.InitializeAsync()` keep their
constructor and setup output buffered until an inherited before-test hook binds
the execution. Disposal also binds an execution when setup fails before the
before-test hook runs, then removes its context and pending output. Tasks started
in constructors share the buffer state: writes after binding go to the owning
run, and writes after disposal are ignored by BDTest capture.

## Asynchronous teardown

xUnit v3 calls `DisposeAsync()` instead of `Dispose()` for tests that implement
`IAsyncDisposable` or `IAsyncLifetime`. The adapter implements `IAsyncLifetime`
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

This also applies when overriding `InitializeAsync()`. Change
an existing `DisposeAsync()` implementation to an override. If you hide the
method or explicitly reimplement `IAsyncDisposable.DisposeAsync()`, you must
still call `await base.DisposeAsync()`; xUnit does not also call `Dispose()`.

See the [xUnit v3 migration guide](https://xunit.net/docs/getting-started/v3/migration#iasynclifetime-now-inherits-from-iasyncdisposable)
for the runner's disposal behavior.
