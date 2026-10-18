---
title: Composition host
description: Use context, scenarios, and output helpers without inheriting a BDTest base class.
---

# Composition host

`BDTestScenario.For<TContext>(testInstance, run)` creates a host for a test class with any base class. The host provides the same `Context`, `Given`, `When`, scenario outlines, and output helpers as the inheritance API. Read context through the host inside each step so retries use the new context.

```csharp
using BDTest;
using BDTest.Test;

public class CheckoutTests : CompanyTestBase
{
    private readonly BDTestScenario<CheckoutContext> _bdt;

    public CheckoutTests(BDTestRun run)
    {
        _bdt = BDTestScenario.For<CheckoutContext>(this, run);
    }

    public async Task Pays(string frameworkExecutionId)
    {
        // Call synchronously before the first await, including asynchronous setup.
        _bdt.BeginTest(frameworkExecutionId);
        try
        {
            _bdt.WriteStartupOutput("Preparing checkout");
            await _bdt.Given(() => _bdt.Context.AddItem("x"))
                .When(() => _bdt.Context.Pay())
                .Then(() => _bdt.Context.VerifyReceipt())
                .BDTestAsync();
        }
        finally
        {
            _bdt.WriteTearDownOutput("Checkout finished");
            _bdt.EndTest();
        }
    }
}
```

The example shows manual lifecycle integration. It does not register framework hooks or inject the run automatically. Framework-native composition registration is tracked separately for [NUnit](https://github.com/thomhurst/BDTest/issues/895), [xUnit v3](https://github.com/thomhurst/BDTest/issues/896), [TUnit](https://github.com/thomhurst/BDTest/issues/897), and [MSTest](https://github.com/thomhurst/BDTest/issues/898). Existing adapter base classes remain available.

## Lifecycle and parallel execution

Create the host with the actual test instance. Story attributes, scenario method attributes, tags, test information, and attributed retry setup/teardown methods come from that instance, including inherited retry hooks. Caller member/file information still comes from the call to `Given`, `When`, or `ScenarioOutline`. Place those calls in the test method when its attributes should supply metadata. Retries recreate context through the same implementation used by the base classes. Configure step hooks through [run options](../advanced/explicit-runs.md).

Call `BeginTest` synchronously before asynchronous setup. Use a framework execution ID that distinguishes parallel tests, data rows, and repeats. Call `EndTest` in teardown even when setup or the test throws. Repeated `EndTest` calls are safe. Completion clears the execution's context and pending output state, retaining recorded scenarios for reports.

`[BDTestRetry]` also comes from the owner: the scenario method takes precedence over the owner class and its assembly. This lookup continues to use the owner after asynchronous steps suspend and the scenario method returns.

Preserve the execution flow across setup, test body, and teardown. A fixture instance also retains the last execution for runners that cross execution-context boundaries. If a runner crosses those boundaries and runs several tests on one fixture concurrently, use a separate host for each execution. Do not start two executions with the same ID on the same host.

## Run ownership and outcomes

Pass an [explicit run](../advanced/explicit-runs.md) to isolate scenarios, contexts, and captured output. Different runs can use the same framework execution ID. The host does not dispose the run: dispose it after all tests, teardown, and output writers finish. Creating a host for a disposed run throws. Omitting the run selects the legacy default run and its global settings.

Register the adapter's `OutcomeMap` on `BDTestOptions` before starting an explicit run to classify framework exceptions and notify the runner about whole-scenario skips. Without an outcome map, the standalone host returns skipped scenarios for the caller to inspect. See [exception classification](../configuration/exceptions.md) and [custom adapters](custom-adapters.md).
