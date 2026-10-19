---
title: NUnit
description: Use BDTest with NUnit through composition or an optional base class.
---

# NUnit

The adapter uses a [configured assembly run](../configuration/assembly-runs.md). Configure `BDTestOptions` with `[assembly: BDTestConfiguration(typeof(TestConfiguration))]`. Use `NUnitBDTestAssembly.GetRun(typeof(MyTests).Assembly)` for reports and call `NUnitBDTestAssembly.Dispose(assembly)` after all tests and exports finish. Explicit-run overrides remain supported.


```bash
dotnet add package BDTest.NUnit
```

`BDTest.NUnit` targets `net8.0` and depends on NUnit 5. Add `Microsoft.NET.Test.Sdk` and `NUnit3TestAdapter` to the test project as usual.

## Composition with an existing base class

Use `[BDTestFixture]` and implement `INUnitBDTest`. Create one stable NUnit host for the actual fixture instance using its assembly run (or pass an explicitly owned run):

```csharp sample="NUnit/Composition.cs" title="Compiled example"
using BDTest;
using NUnit.Framework;

[BDTestFixture]
public class ComposedBasketTests : CompanyTestBase, INUnitBDTest
{
    private readonly NUnitBDTestScenario<BasketContext> _bdt;

    public ComposedBasketTests()
    {
        _bdt = NUnitBDTestScenario.For<BasketContext>(this);
    }

    BDTestBase INUnitBDTest.BDTestHost => _bdt;

    [SetUp]
    public async Task Prepare()
    {
        await _bdt.InitializeContextAsync();
        Console.WriteLine("Preparing basket");
    }

    [Test]
    public async Task AddingAnItem()
    {
        await _bdt.When(() => _bdt.Context.Add("Book", 12.50m))
            .Then(() => _bdt.Context.VerifyTotal(12.50m));
    }
}
```

`BDTestFixture` uses NUnit 5's `IWrapSetUpTearDown` extension. It binds the host before any per-test setup and completes it after all per-test teardown, including setup/body/teardown failures. Completion awaits owned async context disposal and preserves both runner failures and disposal diagnostics. Do not call `BeginTest` or `EndTest` from fixture hooks when using this registration.

The host derives from the [core composition host](composition.md). Context factories, initialization, retries, outlines, console capture and reports use the same core implementation. Contexts need no parameterless constructor when the run supplies a context factory. Read `_bdt.Context` inside each step so retries use the replacement context. To customize scenario or step hooks, derive from `NUnitBDTestScenario<TContext>` and call its protected constructor with the fixture instance and run. Register run-wide hooks on `BDTestOptions`.

A fixture may share its host across parallel test methods: NUnit preserves the wrapper's execution flow, and each row/repeat receives a distinct identity. Alternatively, use `[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]`. Return the same host on every `BDTestHost` access; a host created for another fixture is rejected. Constructor and one-time fixture output are outside per-test capture.

NUnit selects method-level setup/teardown wrappers before class-level wrappers. If a test method declares another `IWrapSetUpTearDown` attribute, also put `[BDTestFixture]` on that method. Avoid combining independently managed BDTest hosts on one test.

## Writing tests

Optionally, inherit from `NUnitBDTestBase<TContext>`:

```csharp sample="NUnit/Inheritance.cs" title="Compiled example"
using BDTest;
using NUnit.Framework;

[Story(AsA = "shopper", IWant = "to add items to my basket", SoThat = "I can buy them")]
public class BasketTests : NUnitBDTestBase<BasketContext>
{
    private BasketSteps Basket => new(Context);

    [Test]
    [ScenarioText("Adding an item updates the total")]
    public async Task AddingAnItem()
    {
        await Given(() => Basket.IsEmpty())
            .When(() => Basket.Add("Book", 12.50m))
            .Then(() => Basket.TheTotalIs(12.50m));
    }
}
```

`[TestFixture]` is already on the base class, and you can run fixtures and tests in parallel with `[Parallelizable]`.

Contexts may have constructor dependencies: override `CreateContextAsync(ContextCreation)` to construct them. BDTest awaits `IBDTestContextLifetime.InitializeAsync()` before scenario steps and disposes owned contexts at teardown, preferring `IAsyncDisposable` over `IDisposable`. Call `await InitializeContextAsync()` when async fixture setup needs an initialized context before any scenario. See [Test context](../writing-tests/test-context.md) for factories, retries and ownership.

## What the adapter does

- **Execution ID:** `TestContext.CurrentContext.Test.ID` plus the current repeat count, so `[Repeat]` runs stay separate.
- **Exceptions:** registers `SuccessException` (from `Assert.Pass`) as success, `IgnoreException` (from `Assert.Ignore`) as skip and `InconclusiveException` as inconclusive. The adapter registers these outcomes only on its assembly run. Explicitly owned runs register them through options before starting.
- **Lifecycle:** the base class supplies the same `BDTestFixture` registration and lifecycle path as composition. Capture starts before setup and finishes after teardown. Its callable `Setup`, `PruneContext` and `BDTestNUnitTearDown` methods remain compatibility shims for manual harnesses. Existing `MarkTestAsComplete()` overrides remain supported through the synchronous compatibility path.
- **Retries:** each attempt runs its steps and step hooks through NUnit's public exception constraint API. A failed attempt does not change the NUnit result when a later attempt passes. Failures outside the scenario remain failures, and scenario warnings remain visible. When the final attempt throws after recording other assertion failures, NUnit reports the exception together with those assertion diagnostics. Retry rules still receive the original exception. The adapter never overwrites the NUnit result during teardown.
- **Skips:** a whole-scenario skip calls `Assert.Ignore(reason)`.

You can add your own `[SetUp]` and `[TearDown]` methods. They run alongside the adapter's.

## Reports after the run

Add `BDTest.Reporting` at the same version as the adapter before using these export APIs.
See [reporting package migration](../migration/reporting-package.md).

Inheritance and the no-run composition overload share `NUnitBDTestAssembly.GetRun(assembly)`. Pass that run explicitly to report helpers. The adapter completes each test but leaves run disposal to assembly cleanup, after reporting. Reused hosts must dispose the previous assembly run and create new fixtures for the next execution.

Use an NUnit `[SetUpFixture]` with no namespace so it covers the entire test assembly to run code after all tests:

```csharp sample="NUnit/AssemblyReport.cs" title="Compiled example"
using BDTest;
using BDTest.Helpers;
using NUnit.Framework;

[SetUpFixture]
public class BDTestReports
{
    [OneTimeTearDown]
    public async Task AfterAllTests()
    {
        var assembly = typeof(BDTestReports).Assembly;
        var run = BDTest.NUnitBDTestAssembly.GetRun(assembly);
        var descriptor = new BDTestRunDescriptor { Environment = "CI", Tag = "Acceptance" };

        try
        {
            await File.WriteAllTextAsync("BDTest-Report.html",
                BDTestHtmlHelper.GetTestHtmlReport(descriptor, run));

            // Or upload to a report server before disposing the run:
            // var result = await uploader.UploadReportAsync(run, descriptor);
        }
        finally
        {
            BDTest.NUnitBDTestAssembly.Dispose(assembly);
        }
    }
}
```

See [HTML report](../reports/html-report.md) and [Sending results](../report-server/sending-results.md).

The adapter supplies the runner's exact test method to `BeginTest(id, methodInfo)`. Scenario text, information, tags and retry attributes use cached method metadata, including when a helper constructs the scenario after an await. See [custom adapter migration](custom-adapters.md#method-identity-and-migration).

## Migrating to composition

1. Keep your existing company base class and add `[BDTestFixture]` plus `INUnitBDTest`.
2. Replace inherited builder/context access with a host created for `this` and the owning run. Return it from `INUnitBDTest.BDTestHost`.
3. Remove manual BDTest lifecycle calls. Keep ordinary NUnit setup/teardown methods; the wrapper surrounds them.
4. Move base-class hook overrides to a derived NUnit host or run options. Keep `[Story]`, `[ScenarioText]`, tags and retry attributes on the fixture/method; the wrapper supplies exact method metadata, including overloads, rows and repeats.
5. Generate reports from the explicit run, then dispose it after assembly teardown.

Existing `NUnitBDTestBase<TContext>` fixtures need no migration. To use an explicit run with inheritance, override `TestRun` with the same stable run owner.

## Cancellation

The inherited base and the framework-specific composition factory automatically forward
`NUnit.Framework.TestContext.CurrentContext.CancellationToken` to scenario and outline execution, token-aware steps, and hooks, even when no
BDTest timeout is configured. This API is verified against NUnit 5.0.0. Use
`[CancelAfter(...)]` or runner cancellation to signal the framework token.

`BDTestAsync(callerToken)` combines your token with the framework token; passing a caller
token does not disable runner cancellation. Either source stops execution before unreached
steps or retries start. BDTest records `Failed` with an `OperationCanceledException` and a
cancellation reason; framework cancellation alone does not set BDTest's `TimedOut` flag.
The enclosing framework may also report its own timeout or cancellation outcome.

Use the framework-specific factory for automatic forwarding. A core-only
`BDTestScenario.For<TContext>(this, run)` host has no framework token; pass the runner token
to `BDTestAsync(token)` explicitly when retaining that host. Steps must observe their token;
BDTest cannot interrupt arbitrary synchronous code. See [timeouts](../writing-tests/timeouts.md).
