---
title: Standalone
description: Use BDTest without a framework adapter through BDTestBase and BDTestBuilder.
---

# Standalone

The core `BDTest` package works without an adapter. Use it with a framework that has no adapter, in a console app, or when you only want the builder syntax.

```bash
dotnet add package BDTest
```

## `BDTestBase`

Inherit `BDTestBase` for the `Given` / `When` / `Then` methods without a context:

```csharp title="Illustrative fragment"
public class SmokeTests(BDTestRun run) : BDTestBase
{
    public override BDTestRun TestRun => run;

    public async Task HomePageLoads()
    {
        await When(() => Browser.OpenTheHomePage())
            .Then(() => Browser.TheTitleIs("Welcome"))
            .BDTestAsync();
    }
}
```

Create a [local context](../writing-tests/test-context.md#inline-contexts) for per-test state.

For a typed context without inheriting a BDTest base class, use the [composition host](composition.md). It also associates metadata and retry hooks with your test instance and accepts an explicit run.

## `BDTestBuilder`

`BDTestBuilder` is a `BDTestBase` that you create instead of inheriting. Each builder gets its own execution ID when it is constructed, so output written from async setup or `Task.Run` before the scenario uses the same ID as the scenario. **Create a new builder for each test.**

```csharp title="Illustrative fragment"
using var run = BDTestRun.Start();
var scenario = await new BDTestBuilder(run)
    .Given(() => Steps.Prepare())
    .When(() => Steps.Act())
    .Then(() => Steps.Verify())
    .BDTestAsync();

Console.WriteLine(scenario.Status);
```

Export reports before disposing the run. Custom adapters must implement `TestRun` with a stable owned run; there is no implicit default store. See [explicit runs](../advanced/explicit-runs.md).

## Things to know

- No framework exceptions are registered. Register your framework's success, skip and inconclusive exceptions yourself; see [Exception classification](../configuration/exceptions.md).
- A whole-scenario skip does not throw. Inspect the returned `Scenario.Status` instead.
- If your runner runs tests in parallel and you inherit `BDTestBase` directly, give each test a stable execution ID. See [Custom adapters](custom-adapters.md).
