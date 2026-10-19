---
title: Composition host
description: Use context, scenarios, and output helpers without inheriting a BDTest base class.
---

# Composition host

`BDTestScenario.For<TContext>(testInstance, run)` creates a host for a test class with any base class. The host provides the same `Context`, `Given`, `When`, scenario outlines, and output helpers as the inheritance API. Read context through the host inside each step so retries use the new context.

Derive from `BDTestScenario<TContext>` and call its protected constructor with the test instance and run to customize scenario hooks or context creation. Contexts need no parameterless constructor when the run supplies a context factory. Without a factory or a `CreateContextAsync` override, the host creates contexts through their public parameterless constructor.

```csharp title="Illustrative fragment"
using BDTest;
using System.Reflection;

public class CheckoutTests : CompanyTestBase
{
    private readonly BDTestScenario<CheckoutContext> _bdt;

    public CheckoutTests(BDTestRun run)
    {
        _bdt = BDTestScenario.For<CheckoutContext>(this, run);
    }

    public async Task Pays(string frameworkExecutionId, MethodInfo testMethod)
    {
        // Call synchronously before the first await, including asynchronous setup.
        _bdt.BeginTest(frameworkExecutionId, testMethod);
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

The example shows manual lifecycle integration. For automatic setup and completion, use [NUnit composition registration](nunit.md#composition-with-an-existing-base-class), [native xUnit v3 registration](xunit.md#composition-with-an-unrelated-base-class), TUnit's [`IBDTestTUnitTest`](tunit.md#composition-with-an-existing-base-class), or MSTest's [`IBDTestMSTestTest`, `[BDTestClass]`, and context injection](mstest.md#composition-with-an-unrelated-base-class). Existing adapter base classes remain available.

## Lifecycle and parallel execution

Create the host with the actual test instance. Story attributes, class tags, class/assembly retry defaults, and attributed retry setup/teardown methods come from that instance, including inherited retry hooks. Pass the runner's exact `MethodInfo` to `BeginTest(frameworkExecutionId, testMethod)` to bind scenario text, method tags, test information, and method retry attributes. Method identity remains stable across asynchronous continuations and overloads. Caller member/file information still comes from the call to `Given`, `When`, or `ScenarioOutline`. Retries recreate context through the same implementation used by the base classes. Configure step hooks through [run options](../advanced/explicit-runs.md).

`BeginTest(frameworkExecutionId)` remains available for manual harnesses without method identity. It uses owner class/assembly defaults and humanized compiler caller text; it does not guess a method by name or inspect stack frames. Supply explicit scenario text when needed. Existing composition integrations that rely on method attributes must migrate to the `MethodInfo` overload.

Call `BeginTest` synchronously before asynchronous setup. Use a framework execution ID that distinguishes parallel tests, data rows, and repeats. Await `EndTestAsync()` in teardown even when setup or the test throws; this also awaits asynchronous context disposal. `EndTest()` remains a synchronous compatibility method. Repeated completion calls are safe. Completion clears the execution's context and pending output state, retaining recorded scenarios for reports. Framework integrations can call the public `BDTestBase.BeginTest(id, method)` and `EndTestAsync()` methods on either kind of host without friend-assembly access.

`[BDTestRetry]` also comes from the owner: the bound test method takes precedence over the owner class and its assembly. Cached metadata continues to apply after asynchronous steps suspend and the scenario method returns.

Preserve the execution flow across setup, test body, and teardown. A fixture instance also retains the last execution for runners that cross execution-context boundaries. If a runner crosses those boundaries and runs several tests on one fixture concurrently, use a separate host for each execution. Do not start two executions with the same ID on the same host.

## Run ownership and outcomes

Pass an [explicit run](../advanced/explicit-runs.md) to isolate scenarios, contexts, and captured output. Different runs can use the same framework execution ID. The host does not dispose the run: dispose it after all tests, teardown, and output writers finish. Creating a host for a disposed run throws. The core `BDTestScenario.For` requires a run. The framework-specific `NUnitBDTestScenario`, `XUnitBDTestScenario`, `MSTestBDTestScenario` and `TUnitBDTestScenario` factories instead select the [configured assembly run](../configuration/assembly-runs.md).

Register the adapter's `OutcomeMap` on `BDTestOptions` before starting an explicit run to classify framework exceptions and notify the runner about whole-scenario skips. Without an outcome map, the standalone host returns skipped scenarios for the caller to inspect. See [exception classification](../configuration/exceptions.md) and [custom adapters](custom-adapters.md).

## Framework cancellation

`NUnitBDTestScenario`, `XUnitBDTestScenario`, `MSTestBDTestScenario`, and
`TUnitBDTestScenario` forward the current test's cancellation token automatically,
including when no BDTest timeout is set. This applies to their explicit-run and
assembly-run overloads. Tokens are read at execution time, so constructing a host
before test setup does not capture a stale token. Keep the lifecycle registration
shown on each framework page.

The core `BDTestScenario.For` factory remains framework-independent. To retain it,
pass your framework's token to `BDTestAsync(token)`; to enable automatic forwarding,
replace the factory with the corresponding framework factory. An explicit caller
token passed to a framework host is combined with its runner token.
