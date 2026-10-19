---
title: Assembly runs
description: Configure, report and dispose isolated runs owned by the built-in adapters.
---


Install `BDTest.Reporting` alongside your adapter to export JSON, generate HTML or upload reports.
Use matching package versions; see [reporting package migration](../migration/reporting-package.md).
# Assembly runs

The NUnit, xUnit, MSTest and TUnit adapters share one `BDTestRun` per concrete test assembly and framework. Different assemblies, including assemblies with the same name loaded separately, get separate options, scenario stores, captured output and report IDs. Different context types in the same assembly share a run.

## Configure before tests start

Add one assembly attribute selecting a public configuration class with a public parameterless constructor:

```csharp sample="TUnit/Configuration.cs" title="Compiled example"
using BDTest;

[assembly: BDTestConfiguration(typeof(TestConfiguration))]

public sealed class TestConfiguration : IBDTestConfiguration
{
    public void Configure(BDTestOptions options)
    {
        options.RetryTestRules.Add(exception => exception is IOException, retryLimit: 2);
        options.SkipTestRules.Add(scenario => scenario.Tags.Contains("manual"));
        // options.RegisterCustomStringConverter(new MoneyConverter());
        // options.StepHooks.Add(new LoggingHook());
    }
}
```

The first run lookup invokes configuration once, registers the adapter's outcome map, and freezes the options. Later changes to the options do not affect that run. Configuration must finish synchronously and must not look up or dispose assembly runs, including through another thread. If configuration throws, no run is published and a later lookup can retry. A fresh configuration instance is created after disposal.

Hooks, converters, context factories and service providers are caller-owned references. They must support concurrent tests and outlive the run. See [run options](../advanced/explicit-runs.md#run-options).

## Select and report the run

| Framework | Assembly owner | Composition factory using the assembly run |
| --- | --- | --- |
| NUnit | `BDTest.NUnitBDTestAssembly` | `NUnitBDTestScenario.For<TContext>(this)` |
| xUnit | `BDTest.XUnitBDTestAssembly` | `XUnitBDTestScenario.For<TContext>(this)` |
| MSTest | `BDTest.MSTestBDTestAssembly` | `MSTestBDTestScenario.For<TContext>(this)` |
| TUnit | `BDTest.TUnitBDTestAssembly` | `TUnitBDTestScenario.For<TContext>(this)` |

Base-class adapters resolve the assembly of the actual derived fixture. Framework-specific composition factories resolve the assembly of the supplied test instance; keep the framework lifecycle registration described on its framework page. Explicit-run overloads and `TestRun` overrides still select caller-owned runs and bypass assembly configuration. Such callers register the framework's outcome map before `BDTestRun.Start(options)`.

Always pass the selected run to report and utility helpers:

```csharp title="Illustrative fragment"
var assembly = typeof(MyTests).Assembly;
var run = NUnitBDTestAssembly.GetRun(assembly);
var descriptor = new BDTestRunDescriptor { Environment = "CI", Tag = "Acceptance" };
var scenarios = BDTestUtil.GetScenarios(run);
var json = BDTestJsonHelper.GetTestJsonData(descriptor, run);
var html = BDTestHtmlHelper.GetTestHtmlReport(descriptor, run);
// await BDTestReportServer.SendDataAndGetReportUriAsync(run, serverUri, descriptor);
```

## Cleanup and reused hosts

The assembly host owns cleanup. After **all tests, teardown, background output writers, attachment readers and report exports** finish, call the matching owner's `Dispose(assembly)` in a `finally` block. Use NUnit's assembly-wide `SetUpFixture` teardown, an xUnit assembly fixture's disposal, MSTest's `AssemblyCleanup`, or TUnit's `After(HookType.Assembly)`. Do not dispose from an individual fixture or test cleanup: other tests still share the run. Run disposal does not wait for or cancel active tests.

For NUnit, put this fixture outside any namespace so cleanup covers the whole assembly:

```csharp title="Illustrative fragment"
[SetUpFixture]
public sealed class Reports
{
    [OneTimeTearDown]
    public void Finish()
    {
        var assembly = typeof(Reports).Assembly;
        try
        {
            var run = NUnitBDTestAssembly.GetRun(assembly);
            File.WriteAllText("report.html", BDTestHtmlHelper.GetTestHtmlReport(null, run));
        }
        finally { NUnitBDTestAssembly.Dispose(assembly); }
    }
}
```

Disposal is synchronous and clears stores, buffers and temporary attachment files. Repeating disposal without a new lookup is harmless. Dispose through the assembly owner, **not directly through the returned run**: the owner also removes its cached entry. The next lookup creates a fresh report ID and empty run. Reused hosts must perform this cleanup between assembly executions and construct new fixtures/hosts; existing fixtures keep their original run and cannot adopt the next run silently. A host that omits cleanup keeps its run until the assembly is collected or the process exits, and temporary attachment files require explicit disposal.

Custom adapters can own a `BDTestAssemblyRuns` registry with their own `FrameworkOutcomeMap`, or own explicit runs directly.

## Migration from global settings

Built-in adapters no longer read `BDTestSettings` execution settings or subscribe to its static hooks. Move rules, exception registrations, converters and hooks into `IBDTestConfiguration.Configure`; replace `GlobalRetryTestRules`, `GlobalSkipTestRules` and `GlobalSkipStepRules` with their `BDTestOptions` equivalents. Replace static hook subscriptions with `IStepHook`/`IScenarioHook` registrations. `SetupNUnitExceptions` and `SetupTUnitExceptions` are removed: adapter-owned runs register their framework outcomes automatically.

Report and utility helpers require the assembly run explicitly. Custom adapters must implement `BDTestBase.TestRun`; standalone builders require `new BDTestBuilder(run)` and composition requires `BDTestScenario.For<TContext>(testInstance, run)`. There is no implicit default run. Report metadata and custom properties belong on `BDTestRunDescriptor`. See [migration](../migration/explicit-runs.md).
