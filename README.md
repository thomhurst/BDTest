# BDTest
## A testing and reporting framework for .NET

[![nuget](https://img.shields.io/nuget/v/BDTest.svg)](https://www.nuget.org/packages/BDTest/)
[![Codacy Badge](https://api.codacy.com/project/badge/Grade/976b0c6b323b43ef94334f503af9b737)](https://www.codacy.com/app/thomhurst/BDTest?utm_source=github.com&amp;utm_medium=referral&amp;utm_content=thomhurst/BDTest&amp;utm_campaign=Badge_Grade)
[![CodeFactor](https://www.codefactor.io/repository/github/thomhurst/bdtest/badge)](https://www.codefactor.io/repository/github/thomhurst/bdtest)
![Nuget](https://img.shields.io/nuget/dt/BDTest)
[![Docs](https://img.shields.io/badge/docs-thomhurst.github.io%2FBDTest-1f8a52)](https://thomhurst.github.io/BDTest/)

### Packages

Package IDs remain stable for the next major version: `BDTest`, `BDTest.NUnit`,
`BDTest.xUnit` (xUnit v3), `BDTest.MSTest`, `BDTest.TUnit`,
`BDTest.SourceGenerator`, `BDTest.Analyzers`, and `BDTest.Razor.Reports`.
The report package is displayed as **BDTest - ASP.NET Core Reports**; its namespaces
and static asset paths stay unchanged. `BDTest.ReportGenerator.RazorServer` remains
an unpacked sample host. Future storage packages use `BDTest.Reports.Storage.*`,
with `AzureBlob` and `FileSystem` planned; these are not currently published packages.
No package rename or deprecation is part of this policy.

Projects ship only when they explicitly set `IsPackable=true`. See
[BUILDING.md](BUILDING.md#package-selection-and-naming-858) for packing rules and
[installation](docs/docs/getting-started/installation.md) for package targets.

### Support

If you like using BDTest, consider buying me a coffee.

<a href="https://www.buymeacoffee.com/tomhurst" target="_blank"><img src="https://www.buymeacoffee.com/assets/img/custom_images/orange_img.png" alt="Buy Me A Coffee" style="height: auto !important;width: auto !important;" ></a>

### Documentation

Full documentation lives at **[thomhurst.github.io/BDTest](https://thomhurst.github.io/BDTest/)**: installation, writing tests, framework adapters, settings, reports and the report server.

### Repository tests

Run `dotnet run --project BDTest.Pipeline.Tests -c Release` for pipeline tests,
`dotnet run --project BDTest.ReportGenerator.Tests -c Release` for report-host tests, and
`dotnet run --project BDTest.TUnit.Tests -c Release` for the TUnit adapter suite.
The core, NUnit sample, xUnit and MSTest suites still use
`dotnet test <project> -c Release`. CI selects TUnit from evaluated package
references and writes TRX results for both runners. See
[contributing](docs/docs/contributing.md#running-tests) for filters and build commands.

### Report JSON migration

Report serialization uses source-generated System.Text.Json metadata. See
[the serialization migration guide](SERIALIZATION.md) for the new JSON shape,
historical report support, and host configuration changes.

### What is it?
BDTest is a testing and reporting framework focusing on 'Behaviour Driven Testing' ideologies. 
It's a way to structure and run your tests so that:
- They are clear, concise and easy to understand
- They are easy to run and debug
- They don't share state. They are independent, side-effect free and able to run in parallel, meaning faster and more stable tests!
- They translate into business criteria and focus on how the system under test would be used by an end user and their behaviours
- You have an easy way to see test results and share them with your team or business

BDTest can be used standalone, or alongside an existing test running framework. For best results, I recommend TUnit, and installing the BDTest.TUnit package alongside the core BDTest package. With TUnit, inherit from `TUnitBDTestBase<TContext>`.

Packages are also available for other frameworks: BDTest.NUnit, BDTest.xUnit and BDTest.MSTest.

#### Why?
I was originally inspired to build BDTest due to SpecFlow.
My team at work used SpecFlow (as does much of the .NET based industry) for their tests when I joined, and upon using it, realised I really didn't enjoy using it and found it to be difficult to work with, finnicky and long-winded.
It turned out, my team also really disliked it, so I thought I'd change things.

Think of BDTest as a pure code-based alternative to SpecFlow.

### What's the benefit over SpecFlow?
- Passing in more complicated arguments to tests. Enums, models, etc.
- You get the benefit of separating steps into Given, When, Then. However you can use and re-use existing steps without redefining or attributing them as Given step, or a When step, etc.
- No generated test bodies or Gherkin compilation. The optional `BDTest.SourceGenerator` package records step metadata while your delegates execute directly.
- Easier to debug. Test has failed and thrown an exception? The stacktrace should take you straight to where it happened. No being taken to strange auto-generated classes that you don't care about!
- Step into all your code. I used Rider as my IDE, which didn't support SpecFlow, so I couldn't step into my test methods. I had to do a painful Find All! Now as it's all code, I can step straight into my test methods.
- No learning and maintaining separate Gherkin files!

### But non-technical team members can write tests using SpecFlow
- Does that EVER actually happen though?
- They can only use the high level methods. Any new methods, a developer will still need to code the steps for them. If they can only use the high level methods, make the method names human readable, and they can still use them!

### I need reports that translates to business acceptance criteria
- BDTest does that!
- BDTest produces you a clear, easy to understand report, showing what failed and what passed, with output and exceptions from tests automatically captured
- Mark your classes and methods with [Story], [ScenarioText], and [StepText] attributes, providing the output you want, and your reports will produce clean text output
- With `BDTest.SourceGenerator`, simple calls use `[StepText]` or a humanized method name. Other bodies and projects without the generator use caller source text; use `.WithStepText(...)` for a custom sentence.
- If you use the report server, you can just send the relevant party a URL directly to your test report!

## Where can I use it?
The core `BDTest` package targets `netstandard2.0;net8.0`. Adapter and report-server
requirements differ:

| Package | Target framework | Test framework dependency |
| --- | --- | --- |
| `BDTest` | `netstandard2.0;net8.0` | None |
| `BDTest.NUnit` | `net8.0` | NUnit 5 |
| `BDTest.xUnit` | `netstandard2.0;net8.0` | xUnit v3 |
| `BDTest.MSTest` | `netstandard2.0;net8.0` | MSTest 4 |
| `BDTest.TUnit` | `netstandard2.0;net8.0` | TUnit |
| `BDTest.Razor.Reports` | `net10.0` | ASP.NET Core report hosting |
| `BDTest.SourceGenerator` | `netstandard2.0` analyzer | Roslyn 4.8+ |
| `BDTest.Analyzers` | `netstandard2.0` analyzer | Roslyn 4.8+ |

.NET 8+ consumers select the modern runtime assets automatically. Older consumers
keep the .NET Standard assets. Compiler extensions retain their compiler-host target
and analyzer packaging. See the [target migration guide](https://thomhurst.github.io/BDTest/docs/migration/target-frameworks)
for clock configuration, compiler polyfills, and package validation.

Choose a test target supported by both the adapter and its test framework. A
`netstandard2.0` adapter target alone does not guarantee that its test framework
or runner supports .NET Framework. The NUnit example below requires .NET 8 or later;
the repository's test projects use .NET 10.

## Delegate steps in the next major version

Step builders now accept `Action`, `Func<Task>`, `Func<ValueTask>`, and cancellation-aware async delegates, including `ValueTask<T>` results. Statement bodies, `await`, null-conditionals, and patterns are supported. Consumers need C# 13 or newer (.NET SDK 9 or newer); the core targets `netstandard2.0;net8.0`.

Install the optional `BDTest.SourceGenerator` package in each project containing steps to preserve method names, `[StepText]`, skip attributes, and supported automatic argument capture. Use `.WithArgs(...)` for captured locals and unsupported argument expressions, or `.WithStepText(...)` for a custom sentence. See the [migration guide](https://thomhurst.github.io/BDTest/docs/migration/delegate-steps) and [compiler metadata reference](https://thomhurst.github.io/BDTest/docs/advanced/source-generator).

## xUnit v3 migration for the next major version

`BDTest.xUnit` now requires xUnit v3. This is a breaking change in the next major
BDTest release; existing xUnit v2 projects must migrate before upgrading.
The adapter remains signed and targets `netstandard2.0;net8.0`.

Replace the `xunit` 2.x package with `xunit.v3.mtp-off` (currently `4.0.2`) to keep
using VSTest, and set `<OutputType>Exe</OutputType>` in the test project. Keep
`Microsoft.NET.Test.Sdk` and `xunit.runner.visualstudio` (4.x). The repository uses
this setup with `dotnet test`, so no pipeline runner change is required. Projects
that choose Microsoft.Testing.Platform instead can use `xunit.v3` and follow
[xUnit's runner configuration](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform).
The version number `4.0.2` belongs to the xUnit v3 framework package family.

Inherit from `XUnitBDTestBase<TContext>` without a constructor argument:

```csharp
using BDTest.Test;
using BDTest.xUnit;
using Xunit;

public class CheckoutTests : XUnitBDTestBase<CheckoutContext>
{
    [Fact]
    public async Task CompletesCheckout()
    {
        await Given(() => Context.AddItem())
            .When(() => Context.Checkout())
            .Then(() => Assert.True(Context.IsComplete))
            .BDTestAsync();
    }
}
```

Remove constructors that only passed `ITestOutputHelper` to `base(...)`. If the
helper is still needed for output, use `Xunit.ITestOutputHelper` instead of
`Xunit.Abstractions.ITestOutputHelper` and keep it in your derived class only.
Execution IDs come from the public `TestContext.Current.Test.UniqueID`, so theory
rows have separate IDs and contexts even when their data is not serializable.
Runtime BDTest skip rules now produce skipped xUnit results instead of throwing
`NotSupportedException`.

The adapter implements `IAsyncLifetime`. For asynchronous teardown, override
`DisposeAsync()` and call `await base.DisposeAsync()` in a `finally` block, including
when overriding `InitializeAsync`. Prefer the virtual override to reimplementing
`IAsyncLifetime`. Existing interface implementations bind buffered startup output
through an inherited before-test hook. xUnit v3 calls only asynchronous disposal when
both disposal interfaces are present; hiding or explicitly reimplementing
`DisposeAsync()` must also invoke the base cleanup.
See the [xUnit adapter guide](https://thomhurst.github.io/BDTest/docs/frameworks/xunit) for the teardown pattern.

## Status migration for the next major version

New scenarios and steps use `Status.NotRun`, which is also `default(Status)`.
Running scenarios and steps use `Status.Pending`. Retries reset the scenario and
its steps to `NotRun` before retry hooks run. Before-step hooks see `Pending`.
Steps after a failure remain `NotRun`; whole-scenario skips still mark steps
`Skipped`. `Inconclusive` now means a registered inconclusive exception occurred.
Replace checks that used `Inconclusive` to detect unreached steps with `NotRun`.

HTML and Razor reports show "Not run" and "Pending" separately from inconclusive
results. Update report consumers and status switches to accept both new values.
`GetOrder()` ranks statuses as Failed, NotImplemented, Inconclusive, Skipped,
NotRun, Pending, Passed. Mixed report summaries continue to let passes override
skips, while unfinished results take precedence over passes.

The numeric enum values change. Store status names instead of integers. BDTest's
Newtonsoft.Json serialization writes names, including in run summaries. Its reader
preserves historical named statuses, including ambiguous `Inconclusive` results,
and maps legacy integers 0–4 to Passed, Failed, Inconclusive, NotImplemented, Skipped.
Integer tokens are always interpreted using that legacy mapping; new numeric enum
values must not be used as JSON status values.

## How do I get started?

Follow the [getting started guide](https://thomhurst.github.io/BDTest/docs/getting-started/installation) on the documentation site.

### Custom step text converters

Implement `BDTest.Interfaces.IStepTextStringConverter<T>` and register the converter
through `BDTest.Settings.BDTestSettings`:

```csharp
var converter = new NumberTextConverter();
BDTestSettings.RegisterCustomStringConverter(converter);
try
{
    // Build steps whose arguments should use this converter.
}
finally
{
    BDTestSettings.RemoveCustomStringConverter(converter);
}

public sealed class NumberTextConverter : IStepTextStringConverter<int>
{
    public string ConvertToString(int value) => $"number {value}";
}
```

`RegisterCustomStringConverter` and `RemoveCustomStringConverter` support concurrent
registration, removal, and step text generation. Each argument uses a stable snapshot
of converters in registration order; the first matching converter wins. Changes made
after that snapshot apply to subsequent argument conversions. Duplicate registrations
are allowed; removal deletes the first equal registration and returns whether one
was found. Both methods reject null converters.

Converter callbacks run outside the registration lock and can run concurrently.
Converters must protect their own mutable state. Removal does not cancel a callback
or exclude a converter from an earlier snapshot; wait for those conversions to finish
before disposing converter resources. Text is rendered once per executed attempt, so
registration changes apply to later renders, including retries, but do not rewrite completed report text.

The existing `CustomStringConverters` property remains a `List<object>` for compatibility.
Direct access is supported during setup before concurrent access begins. For concurrent
legacy access, hold `lock (BDTestSettings.CustomStringConverters)` for every read,
enumeration, and mutation, using the same lock as the registration API and snapshots.
Unsynchronized legacy list mutations are not thread-safe, even when other callers use
the new API.

### Scenario and story tags

Use `[BDTestTag("smoke")]` from `BDTest.Attributes` on a scenario method or a story
class. Apply the attribute multiple times for multiple tags. Class tags apply to
every scenario in that class, including inherited tags. Method tags combine with
class tags; duplicates are removed ignoring case. Tag names are trimmed and cannot
be empty.

```csharp
[BDTestTag("checkout")]
public class CheckoutTests : BDTestBase
{
    [BDTestTag("smoke")]
    public async Task Successful_checkout()
    {
        // Build and execute your Given/When/Then chain here.
    }
}
```

Tags are available through `BuildableTest.Tags` and `Scenario.Tags` and included in
JSON output, including scenarios that have not run. Existing reports without tags
remain supported. In the report's Scenarios or Stories page, choose a tag and press
Apply to filter scenarios before grouping and pagination. Tag matching ignores
case and combines with the existing status filter. Choose All tags to clear the
tag filter. The report-level `BDTestRunDescriptor.Tag` remains separate from these
scenario tags.

### Execution identity and asynchronous setup

Use a new `BDTestBuilder` for each standalone test. Its execution ID is assigned
when it is constructed, so startup output written from async setup or `Task.Run`
uses the same ID as the scenario. Framework adapters record an execution ID once
at test start, keeping async setup, context access, and teardown together.

For a custom adapter inheriting `BDTestBase` or `AbstractContextBDTestBase<TContext>`,
call the protected `BeginTest(frameworkTestId)` synchronously before starting any
async setup, and call `EndTest()` in teardown. The framework ID must be unique per
execution, including each data row, repeat and parallel test. Keep both lifecycle
calls inside the same async entry method so its
execution identity does not leak into the caller:

```csharp
public async Task RunTestAsync(string frameworkTestId)
{
    BeginTest(frameworkTestId);
    try
    {
        await SetupAsync();
        await When(() => ActAsync()).Then(() => Verify()).BDTestAsync();
    }
    finally
    {
        EndTest();
    }
}
```

`BeginTest` binds `TestRun`, installs console capture before the first step, and
clears stale capture state. `EndTest` removes the context and pending output from
the owning run, then clears capture state. Calling `EndTest` again is safe, even
when another run is active. TUnit hooks export the new flow state through
`TestContext.Current.AddAsyncLocalValues()`.

NUnit uses its test ID plus repeat count. TUnit uses `TestContext.Id`. MSTest uses
the class name and data-row display name plus an execution suffix. The current
xUnit v3 adapter uses the framework test ID, including its data-row identity.
Constructor console output is buffered until xUnit calls `IAsyncLifetime.InitializeAsync`, after the
constructor has assigned any owned `TestRun`. The explicit interface hook binds
capture before invoking virtual `InitializeAsync`, so async setup overrides keep
the same output flow as the test body. Override `DisposeAsync` for async
cleanup and call `base.DisposeAsync()` in `finally`.

Calling the start hook inside a separate async setup method or `Task.Run` does
not propagate its `AsyncLocal` values back to the caller. Per-test fixture
instances retain their execution identity across runner setup/body boundaries.
The legacy standalone default still creates an ID on first access. Existing
`BDTestExecutionId` overrides remain supported for compatibility, but custom
adapters should migrate to `BeginTest(id)`. `MarkTestAsStarted()` and
`MarkTestAsComplete()` remain as obsolete compatibility shims. See the
[custom adapter lifecycle guide](https://thomhurst.github.io/BDTest/docs/frameworks/custom-adapters#execution-identity).

### Before and after step hooks

Register asynchronous global hooks during test-run setup:

```csharp
BDTestSettings.BeforeStep += step =>
{
    Console.WriteLine($"Starting: {step.StepText}");
    return Task.CompletedTask;
};

BDTestSettings.AfterStep += async step =>
{
    if (step.Status == Status.Failed)
    {
        await SaveScreenshotAsync(step.ScenarioId, step.Guid);
    }
};
```

Use `BDTest.Settings` for `BDTestSettings` and `BDTest.Test` for `Step` and `Status`.
For per-test hooks, override `OnBeforeStep(Step step)` and `OnAfterStep(Step step)`
on your `BDTestBase` or framework adapter. These methods return `Task` and can
access the test's context. Global handlers may run concurrently for different
tests, so keep their shared state thread-safe. Remove a global handler with `-=`
when its registration is no longer needed.

For each reached step, BDTest awaits global before hooks in registration order,
then the test's before hook, then the step, then the test's after hook, then global
after hooks in registration order. Subscriptions are captured at step entry;
changes during a step apply to subsequent steps. Before hooks stop at the first
exception. Every after hook still runs if a before hook, the step, or another
after hook throws. The original execution exception takes precedence; secondary
after-hook exceptions are logged. Otherwise the first after-hook exception
determines the result using the usual exception and retry rules.

After hooks receive the step's `Status`, `Exception`, `Output`, `StartTime`,
`EndTime`, and `TimeTaken`. Timing includes before hooks and the step, and excludes
after hooks. Output written by hooks is included in the final step output. All
after hooks observe the execution result before after-hook failures are applied.
Hooks run for individually skipped steps and on each retry, but not for steps
unreached after a failure or for a whole scenario skipped before execution.

### Running a scenario once

A runnable Given/When/Then builder supports one execution through `BDTestAsync()`
or direct `await`. Calling either entry point again on the same builder,
including while it is running or after it fails, throws
`BDTest.Exceptions.AlreadyExecutedException` before another scenario is created or
any steps run again. Each continuation (such as `Then.And(...)`) creates an independent
builder with its own execution limit, step state, and report entry, even when the
original builder has already run. Create a new chain or continuation to run another scenario. Configured BDTest
retries still run within the original execution and retain one report entry.

`Scenario.Attempts` retains each completed attempt in execution order, including
initial and final attempts. Each entry has a one-based `Number`, `Status`,
`Exception` (type, message, and stack trace), `StartTime`, `EndTime`, and `TimeTaken`.
Durations measure each attempt; retry hooks run between attempts. `Scenario.Status`,
`Steps`, and `TimeTaken` continue to describe the final attempt, and `RetryCount`
continues to count retries. JSON reports include the history; older reports load
with an empty history. Retried scenarios show a **Retry history** section in both
standalone HTML and Razor reports, so failures remain visible after a passing retry.

If a retry setup, teardown, or `OnBeforeRetry()` hook throws, BDTest stops retrying
and throws `BDTest.Exceptions.ErrorOccurredDuringRetryActionException`, which derives
from `BDTestException`. Its message includes the hook's error message, and
`InnerException` contains the original hook exception. `ToString()` includes the
cause and its stack trace. `GetBaseException()` follows the normal inner exception
chain to the innermost cause. Scenario and retry attempt reports retain the hook exception,
including any nested cause. See [retry hook errors](https://thomhurst.github.io/BDTest/docs/configuration/retry-hook-errors).

### But steps

Use `.But(...)` after a Given, When, or Then step to express a contrast. It accepts
synchronous actions and asynchronous tasks, renders the `But` keyword in step text
and reports, and keeps the current phase of the chain:

```csharp
await Given(() => AccountExists())
    .But(() => AccountIsLocked())
    .When(() => SignInAsync())
    .But(() => RetrySignInAsync())
    .Then(() => AccessIsDenied())
    .But(() => AccountStillExists())
    .BDTestAsync();
```

Repeat `.But(...)`, combine it with `.And(...)` in Given and Then phases, or use
`.WithStepText(() => ...)` to customize its text. A But step in the When phase is
skipped when `BDTestSettings.Debug.ShouldSkipWhenStep` is enabled.

### Step text and argument values

Generated or explicit argument readers run once immediately before each reached step,
after before-step hooks and skip checks. They read current state again on retries.
Construction, reporting, skipped steps, and unreached steps do not evaluate them.
The step delegate still evaluates its own arguments normally, so keep argument
expressions free of side effects. Unreached steps retain placeholders or caller text.
A reader or converter failure becomes `<error: ExceptionType>` and a step diagnostic;
it does not fail the step. `.WithStepText(() => ...)` remains dynamic, and an empty
override falls back to automatic rendering for that attempt.

The optional source generator captures constants and member paths automatically.
Use `.WithArgs(() => value)` for captured locals and unsupported expressions.
Without method metadata, explicit values append to the caller expression text.
See [delegate migration](https://thomhurst.github.io/BDTest/docs/migration/delegate-steps) for the full rules.

Delegate arguments are displayed with `ToString()` without invoking the delegate.
Collections display their elements, and dictionaries implementing `IDictionary`
display `key=value` pairs. Registered `IStepTextStringConverter<T>` converters
take precedence over default formatting and accept values assignable to `T`,
including derived classes and interface implementations. The first matching
registered converter is used; within that converter, an exact type match takes
precedence over an assignable match.

### Scenario outlines and example tables

Define one scenario over typed example rows with `ScenarioOutline`:

```csharp
await ScenarioOutline("Adding numbers", new[]
{
    new { Left = 1, Right = 2, Expected = 3 },
    new { Left = 4, Right = 5, Expected = 9 }
}, row => Given(() => SetNumbers(row.Left, row.Right))
    .WithStepText(() => "numbers <Left> and <Right>")
    .When(() => AddNumbers())
    .Then(() => CheckResult(row.Expected)));
```

Public property names become example-table columns. Matching `<Name>` placeholders
in step text (including `[StepText]` and `.WithStepText(...)`) use that row's values,
formatted with invariant culture. Unknown placeholders stay unchanged; null values
display as `null`. Values are captured when the outline is built.

Rows run sequentially with separate steps, output, status, and retry counts. Each
row starts with a fresh `Context`, including `ContextAmendment`; runner setup and
teardown run once for the outline. Put per-example initialization in Given steps.
During context amendment and factory construction, `BDTestContext.CurrentScenario`
is `null` because the example scenario does not exist yet. Steps and retry context
amendments see the executing row.
The factory must return a new chain from the same test instance for each row.
All rows run even when one fails. Afterward, the first exception for the most
severe outcome is rethrown to the test runner. Passed rows with skipped rows pass;
all-skipped rows skip. Failure, not-implemented, and inconclusive rows take priority.

JSON, standalone HTML, and Razor reports contain one outline with an example table
and per-row results. `BDTestUtil.GetScenarios()` returns the outline; its `Examples`
collection contains the row scenarios and their `ExampleValues`. An outline needs
at least one example and, like a regular chain, runs once through `BDTestAsync()`
or direct `await`.

### Asynchronous steps

Asynchronous step delegates can return `Task`, `Task<T>`, `ValueTask`, or
`ValueTask<T>`. BDTest awaits each result once before running the next step and
reports asynchronous exceptions. Return the operation directly:

```csharp
await When(() => SaveAsync()) // SaveAsync can return Task or ValueTask.
    .Then(() => VerifySaved())
    .BDTestAsync();
```

Cancellation-aware delegates can accept a `CancellationToken` and return `Task`,
`ValueTask`, or `ValueTask<T>`. Ordinary `async` lambdas select the Task overload.
For configured or custom awaitables, write an `async Task` helper that awaits the
operation and use that helper as the step; directly returning these unsupported
awaitables can bind to `Action` and discard their asynchronous work.
Avoid hiding asynchronous work inside a synchronous or `async void` helper.

### Execution and synchronization contexts

`BDTestAsync()` and direct `await` execute steps in order without scheduling a
thread-pool task for each scenario or step. Execution begins inline; when reporting
and all steps complete synchronously, the whole scenario can complete on the calling
thread. Each asynchronous step must finish before the next step starts.

BDTest preserves the caller's synchronization context across steps, hooks, retries,
and outline rows. An asynchronous step resumes on that context unless its own code
opts out, for example with `ConfigureAwait(false)`. Later steps still start on the
caller's context. Configured timeouts explicitly run step bodies on the thread pool
so they can bound synchronous work; see [Step and scenario timeouts](#step-and-scenario-timeouts).
Await the async entry points and keep UI/STA synchronization contexts processing messages.

The next major release removes the synchronous `BDTest()` entry points. Change test
methods from `void` to `async Task` and replace `.BDTest()` with an awaited
`.BDTestAsync()` call, or directly await the chain:

```csharp
[Test]
public async Task Login()
{
    await Given(() => User()).When(() => LogIn()).Then(() => LoggedIn());
}
```

The same migration applies to scenario outlines. Do not replace `.BDTest()` with
`.Result`, `.Wait()`, or `.GetAwaiter().GetResult()`: blocking a single-threaded
context can deadlock. Tests that require thread-pool execution must schedule that
work explicitly. See [release notes](RELEASE_NOTES.md) and the
[migration guide](https://thomhurst.github.io/BDTest/docs/migration/async-only).

Changes to an `AsyncLocal` value inside a step are not a mechanism for passing data to
later steps or back to the caller. Use the test's `Context` object for shared step data.
The scheduling regression tests cover inline completion, ordered asynchronous steps,
ambient-value isolation, and context preservation across steps, hooks, retries, and
outline rows. No throughput or thread-pool-starvation improvement is claimed from these checks.

### Explicit run ownership

Custom adapters and long-lived hosts can own a `BDTestRun` to isolate report storage:

```csharp
using var run = BDTestRun.Start();
var tests = new MyTests(run);
await tests.When(() => tests.Act()).Then(() => tests.Check()).BDTestAsync();
var scenarios = BDTestUtil.GetScenarios(run);
var json = BDTestJsonHelper.GetTestJsonData(new BDTestRunDescriptor(), run);

public class MyTests(BDTestRun run) : BDTestBase
{
    public override BDTestRun TestRun => run;
    public void Act() { }
    public void Check() { }
}
```

Keep `TestRun` stable for the test instance. Builders and scenarios capture their run,
including builders executed later and scenario outline examples. Each run has a new
`ReportId` and separate scenario, unfinished-definition, listener, and output stores.
An explicit run also has its own executor. JSON/HTML report helpers and report uploads
accept a run; for example,
`BDTestReportServer.SendDataAndGetReportUriAsync(run, serverAddress, descriptor)`.
Existing overloads and framework adapters continue to use the legacy default run.

JSON, HTML and uploads use the same report model. All three copy the obsolete
`BDTestSettings.CustomProperties` at export time, including for explicit runs;
HTML displays these values under **Run properties**. Configure this process-wide
dictionary before exporting, without concurrent changes. Descriptor metadata takes
precedence over the legacy settings. Missing metadata stays `null` in JSON and
uploads; HTML displays "Not Specified". See [report output guidance](https://thomhurst.github.io/BDTest/docs/reports/overview).

Dispose a run only after its tests and output writers finish. Disposal clears its stores
and prevents new scenario construction or execution; it does not cancel active tests.
`BDTestUtil.ClearScenarios(run)` clears storage between executions while retaining the
same report ID. Start a new run to get a new ID.

Pass `BDTestOptions` to configure an explicit run:

```csharp
var options = new BDTestOptions();
options.SuccessExceptionTypes.Add(typeof(MySuccessException));
options.RetryTestRules.Add(exception => exception is IOException, retryLimit: 2);
options.SkipTestRules.Add(scenario => scenario.Tags.Contains("manual"));
options.SkipStepRules.Add<MySkipAttribute>(attribute => attribute.ShouldSkip);
options.Debug.ShouldSkipWhenStep = false;
options.RegisterCustomStringConverter<MyValue>(new MyValueConverter());
options.StepHooks.Add(new MyStepHook()); // Implements BDTest.Interfaces.IStepHook.
using var run = BDTestRun.Start(options);
```

`Start(options)` copies exception types, retry and skip rules, debug settings, typed
converters and ordered step hooks. Later changes to the options affect only new runs.
`Start()` uses empty registrations and default debug settings. Explicit runs do not
inherit execution settings from `BDTestSettings`; register any framework exceptions
your custom adapter needs.
Configure options before starting a run, without concurrent mutations. Callback,
converter and hook instances are not cloned and remain responsible for their own
thread safety.

Converters implement `IStepTextStringConverter<T>`. The first registration whose `T`
accepts the argument wins, including explicit interface implementations. Hook methods
`BeforeStepAsync(Step)` and `AfterStepAsync(Step)` return `Task` and run sequentially
in registration order. Run before hooks precede the test instance's `OnBeforeStep`;
run after hooks follow `OnAfterStep`. Hooks also run for reached skipped steps and
retry attempts. Every after hook runs even if another hook fails; an original step
failure takes precedence over after-hook failures.

`BDTestSettings` still configures the legacy default run used by existing adapters.
Assembly-owned adapter lifecycles remain a separate part of #797.

### Report date formatting

Razor report tables and chart labels use the current request culture's short date
pattern. For example, the same date appears as `17/10/2026` in `en-GB`, `10/17/2026`
in `en-US`, and `17.10.2026` in `de-DE`. Times retain their 24-hour format and up to
three fractional second digits; formatting does not convert time zones.

To select dates using the viewer's browser language, configure ASP.NET Core request
localization in your report host before routing and report endpoints:

```csharp
var reportCultures = new[] { "en-GB", "en-US", "de-DE" };
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture("en-GB")
    .AddSupportedCultures(reportCultures)
    .AddSupportedUICultures(reportCultures));
```

The default localization providers check the culture query parameter, culture cookie,
then the `Accept-Language` header, falling back to the configured default. Hosts can
also customize `DateTimeFormat.ShortDatePattern` on the `CultureInfo` instances they
register in `RequestLocalizationOptions.SupportedCultures`. Without request
localization, the host's current culture determines the date format.

### Report server uploads

Report uploads require an API key. Configure a strong secret on both the server and the uploader before upgrading; an unset or blank key disables uploads (`401`). Report reads require their own policy or explicit anonymous opt-in; ping remains public.

For an embedded report server, configure options before building the host:

```csharp
builder.Services.AddBDTestReports(options =>
{
    options.UploadApiKey = configuration["BDTest:UploadApiKey"];
    options.ReportsAuthorizationPolicy = "BDTestReports.Read"; // Register this policy as shown below.
    options.UploadsPerMinute = 60; // Positive limit shared by authenticated clients of this instance.
    options.MaxCompressedUploadBytes = 10L * 1024 * 1024; // Request body bytes before decompression.
    options.MaxUploadBytes = 10L * 1024 * 1024;           // Expanded JSON bytes.
});
```

`AddBDTestReports` returns `OptionsBuilder<BDTestReportOptions>` and includes startup validation. Bind an entire configuration section with `builder.Services.AddBDTestReports().BindConfiguration("BDTest")`. Register extension services with DI, and call parameterless `app.UseBDTestReportServer()` in the request pipeline for security headers. See [Report options migration](docs/docs/migration/report-options.md) when upgrading from middleware configuration.

The bundled Razor server reads `BDTest:UploadApiKey` from configuration (for example the `BDTest__UploadApiKey` environment variable). Keep the secret out of source control and use HTTPS. Send it using the existing `HttpClient` overload:

```csharp
using var client = new HttpClient();
client.DefaultRequestHeaders.Add("X-BDTest-Upload-Key", uploadApiKey);
var reportUri = await BDTestReportServer.SendDataAndGetReportUriAsync(
    serverAddress, new BDTestRunDescriptor(), client);
```

`POST /bdtest/data` checks the key and rate limit before parsing or decompressing JSON. This endpoint accepts plain JSON or `Content-Encoding: gzip`, including chunked requests. Other content encodings or stacked encodings return `415`; malformed gzip or JSON returns `400`. JSON media types and UTF-8/UTF-16 support are unchanged. Other host endpoints are not decompressed by BDTest.

`MaxCompressedUploadBytes` limits request body bytes before decompression (also for plain requests); `MaxUploadBytes` limits decompressed JSON bytes and its UTF-8 representation after transcoding. Both default to 10 MiB, must be positive, and are enforced while streaming; exceeding either returns `413`. `MaxUploadBytes` also bounds historical JSON reference expansion. Configure these options at startup to raise the limits, and raise any reverse-proxy or IIS request-filtering limits that would reject the request first. The endpoint replaces the mutable ASP.NET Core body-size limit with its own byte counter, excluding HTTP chunk framing. Cache capacity remains separate: raising upload limits does not raise `ReportCacheSizeLimitBytes`.

The default rate limit is 60 authenticated attempts per minute per server instance; excess attempts return `429` with `Retry-After`. The built-in uploader waits for that delay (seconds or an HTTP date) before retrying, with at most three upload attempts. For multiple server instances, enforce a shared limit at the gateway as well.

The server ignores client-supplied report IDs and creates a new GUID for each accepted upload. Use the returned URL to locate the report. Re-uploading (including a retry after a lost response) creates a separate report instead of replacing an existing report. Stored models, summaries, and data-receiver callbacks all receive the server-generated ID.

#### Example Test

This complete NUnit example uses an in-memory account balance, so it needs no server
or credentials. Add it to an NUnit test project that references `BDTest.NUnit`,
`NUnit`, `NUnit3TestAdapter`, and `Microsoft.NET.Test.Sdk`. The steps here are
synchronous; await `BDTestAsync()` to run the scenario. Asynchronous steps can
return `Task`.

The same source is compiled and run in
[ReadmeExampleTests.cs](BDTest.Tests/ReadmeExampleTests.cs). From this repository, run:

```sh
dotnet test BDTest.Tests/BDTest.Tests.csproj -c Release --filter "FullyQualifiedName~ReadmeExampleTests"
```

```csharp
using System.Threading.Tasks;
using BDTest.Attributes;
using BDTest.NUnit;
using NUnit.Framework;

namespace BDTest.Tests.ReadmeExample;

[Story(AsA = "account holder",
    IWant = "to withdraw money",
    SoThat = "I can use my savings")]
public class ReadmeExampleTests : NUnitBDTestBase<AccountContext>
{
    // The base class creates a separate Context for each NUnit test.
    private AccountSteps Account => new AccountSteps(Context);
    private AccountAssertionsSteps AccountAssertions => new AccountAssertionsSteps(Context);

    [Test]
    [ScenarioText("Withdrawing money reduces the account balance")]
    public async Task WithdrawMoney()
    {
        await Given(() => Account.CreateAnAccount(100m))
            .When(() => Account.Withdraw(25m))
            .Then(() => AccountAssertions.TheBalanceIs(75m))
            .BDTestAsync();
    }
}

public class AccountContext
{
    public decimal Balance { get; set; }
}

public class AccountSteps
{
    private readonly AccountContext _context;

    public AccountSteps(AccountContext context)
    {
        _context = context;
    }

    [StepText("I have an account with a balance of {0}")]
    public void CreateAnAccount(decimal openingBalance)
    {
        _context.Balance = openingBalance;
    }

    [StepText("I withdraw {0}")]
    public void Withdraw(decimal amount)
    {
        _context.Balance -= amount;
    }
}

public class AccountAssertionsSteps
{
    private readonly AccountContext _context;

    public AccountAssertionsSteps(AccountContext context)
    {
        _context = context;
    }

    [StepText("the account balance is {0}")]
    public void TheBalanceIs(decimal expectedBalance)
    {
        Assert.That(_context.Balance, Is.EqualTo(expectedBalance));
    }
}
```

### Hosting the sample server behind a reverse proxy

Report navigation uses local URLs and preserves `Request.PathBase`. Clipboard sharing resolves these URLs against the browser's origin. Hosts embedding the Razor library should configure their own host filtering and forwarded headers middleware.

The sample `BDTest.ReportGenerator.RazorServer` accepts only `localhost`, `127.0.0.1`, and `[::1]` by default. Set `AllowedHosts` to your deployment's semicolon-separated host names (without schemes, ports, or paths), for example `reports.example.com;report-backend.internal`. Include the backend host if your proxy sends it in the original Host header. The same allowlist constrains `X-Forwarded-Host`; avoid `*`.

Forwarded headers run before HTTPS redirection and authentication. Only loopback proxies are trusted by default. For a remote proxy, set its actual connection IP through `ReverseProxy__KnownProxies__0` (and `__1`, etc. for additional trusted addresses). IPv4-mapped IPv6 connections need the mapped address. The default limit is one forwarding hop; more complex chains require explicit `ForwardedHeadersOptions` configuration in `Program.CreateBuilder`.

Configure the proxy to overwrite client-supplied forwarding headers and send `X-Forwarded-For`, `X-Forwarded-Proto`, and `X-Forwarded-Host`. When stripping a public URL prefix such as `/reports`, also send `X-Forwarded-Prefix: /reports`. If the proxy preserves the prefix in the forwarded request path, configure `UsePathBase("/reports")` before routing instead. Include the prefix in the Entra redirect URI, for example `https://reports.example.com/reports/signin-oidc`. Do not enable blanket forwarding with `ASPNETCORE_FORWARDEDHEADERS_ENABLED` or clear the trusted proxy lists.

See [ASP.NET Core proxy configuration](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0) for deployment-specific trust and path handling.

### Securing a report server

`BDTest.Razor.Reports` denies report reads with HTTP 403 until you set `ReportsAuthorizationPolicy` to a registered ASP.NET Core policy, apply explicit endpoint authorization with `RequireAuthorization(...)`, or explicitly opt in with `AllowAnonymousReports = true`. It logs why access is denied. This covers report pages, lists, raw JSON, attachment downloads and bulk comparisons. Your host supplies authentication schemes and policies; uploads retain their separate API key.

Deletion requires a named `AdminAuthorizationPolicy` in addition to read access and an antiforgery token. An unset admin policy disables deletion, including for public reports. The run list uses the same policy for delete buttons. This major-version change removes `IAdminAuthorizer`, `NoOpAdminAuthorizer` and the `AdminAuthorizer` option: move custom checks into an ASP.NET Core authorization requirement handler.

The sample `BDTest.ReportGenerator.RazorServer` uses Microsoft Entra ID with `Microsoft.Identity.Web` and requires a signed-in user by default. Register a single-tenant web application, register `https://<your-report-host>/signin-oidc` as its redirect URI, and set these values through environment variables or a secret store:

- `AzureAd__TenantId`: your tenant ID.
- `AzureAd__ClientId`: the application's client ID.
- `AzureAd__ClientSecret`: the application's client secret (or configure a certificate through Microsoft.Identity.Web).
- `AzureStorage__ConnectionString`: the existing report storage connection string.
- `BDTest__UploadApiKey`: a long, randomly generated secret shared only with report-uploading test jobs.

For local development, use `dotnet user-secrets set "<Section>:<Key>" "<value>" --project BDTest.ReportGenerator.RazorServer`. Keep secrets out of committed configuration. Configure access to the enterprise application in Entra ID to restrict who may sign in; by default the server allows every authenticated user accepted by that application to read every report. It does not provide per-report permissions.

The upload endpoint, `POST /bdtest/data`, bypasses interactive sign-in but requires the `X-BDTest-Upload-Key` header in the sample host. Missing, incorrect, or multiple keys return HTTP 401; leaving the server's key empty disables uploads. A reader's sign-in cookie cannot replace the upload key, and an upload key does not grant read access. `/bdtest/ping` remains anonymous for warm-up, as does the library's error action. Static CSS, JavaScript, and images remain public.

Pass a configured `HttpClient` to the existing upload overload:

```csharp
using var client = new HttpClient();
client.DefaultRequestHeaders.Add(
    "X-BDTest-Upload-Key",
    Environment.GetEnvironmentVariable("BDTEST_REPORT_UPLOAD_API_KEY"));

var reportUri = await BDTestReportServer.SendDataAndGetReportUriAsync(
    new Uri("https://your-report-host"),
    new BDTestRunDescriptor(),
    client);
```

For your own ASP.NET Core host, register an authentication provider and named read and admin policies. For example, with the `Microsoft.Identity.Web` and `Microsoft.Identity.Web.UI` packages:

```csharp
services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(Configuration.GetSection("AzureAd"));
services.AddAuthorization(options =>
{
    options.AddPolicy("BDTestReports.Read", policy => policy.RequireAuthenticatedUser());
    options.AddPolicy("BDTestReports.Admin", policy =>
        policy.RequireAuthenticatedUser().RequireRole("BDTest.Admin"));
});
services.AddControllersWithViews()
    .AddMicrosoftIdentityUI();
services.AddBDTestReports(options =>
{
    options.ReportsAuthorizationPolicy = "BDTestReports.Read";
    options.AdminAuthorizationPolicy = "BDTestReports.Admin";
    options.UploadApiKey = Configuration["BDTest:UploadApiKey"];
});
services.AddRazorPages();
```

Use the `Microsoft.AspNetCore.Authentication.OpenIdConnect`, `Microsoft.Identity.Web`, `Microsoft.Identity.Web.UI`, and `BDTest.Razor.Reports.Extensions` namespaces. Register storage interfaces with DI and configure `UploadApiKey` through `AddBDTestReports` as shown above. `AddBDTestReports` includes the report controllers and views; its options builder supports configuration binding and validates options on host startup. The request pipeline must select the endpoint before checking its authorization:

```csharp
app.UseHttpsRedirection();
app.UseStaticFiles(); // Only public assets belong in the web root.
app.UseBDTestReportServer(); // Adds security headers under the mapped report prefix.
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseEndpoints(endpoints =>
{
    endpoints.MapBDTestReports().Add(endpoint =>
    {
        var action = endpoint.Metadata.OfType<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>().SingleOrDefault();
        if (action == null || action.ControllerName == "BDTest" && action.ActionName is "Ping" or "Index" or "Error"
            || action.ControllerName == "Attachments" && action.ActionName == "Upload")
            endpoint.Metadata.Add(new Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute());
    });
    endpoints.MapRazorPages();
});
```

The example explicitly permits anonymous uploads, errors and report assets to bypass interactive sign-in. The upload authorization filter requires `X-BDTest-Upload-Key` independently. Configure `UploadApiKey` for every host; no separate upload authentication middleware is needed. Use HTTPS for both browser sessions and uploads.

For intentionally public reports, set `AllowAnonymousReports = true` instead of a read policy. A configured read policy takes precedence over this flag. The flag does not bypass host fallback policies or endpoint `RequireAuthorization(...)`, and grants no delete or upload permission. Explicit host endpoint authorization can also configure read access when no library read policy is set; a global fallback policy alone cannot. The sample sets both named policies above.

See [Microsoft Identity Web setup](https://github.com/AzureAD/microsoft-identity-web/wiki/web-apps) and [ASP.NET Core authorization policies](https://learn.microsoft.com/aspnet/core/security/authorization/policies) for provider configuration and policy customization.

### Report Server Example Image
![Report Server Example Image](https://github.com/thomhurst/BDTest/blob/main/Images/Test%20Runs%20List.png?raw=true)

### Full-report cache capacity and retention

The Razor report server uses a dedicated bounded cache, separate from the host's
`IMemoryCache`. Configure its policy at startup, before resolving the repository
or serving requests:

```csharp
builder.Services.AddBDTestReports(options =>
{
    options.ReportCacheSizeLimitBytes = 64L * 1024 * 1024; // Default: 64 MiB
    options.ReportCacheRetention = TimeSpan.FromHours(8); // Default: eight hours
});
```

Both settings must be positive; invalid options fail host startup with `OptionsValidationException`. Each full report is charged its serialized UTF-8
JSON size when cached, preserving shared object references as in report uploads.
Accounting uses compact JSON with fixed settings, independent of the host's
`JsonConvert.DefaultSettings`.
Size measurement streams the JSON without retaining a complete serialized copy.
The sum of those sizes cannot exceed the configured budget.
This is an estimate of retained report size, not a hard limit on process memory:
object overhead, temporary serialization allocations, in-flight responses, and
report summaries are outside this budget. Treat uploaded/cached models as immutable.
The measured size stays with the cache entry; ordinary reads reuse it without
serializing the report again. Explicit replacement through the cache store measures
the replacement model's size.

Uploads and ordinary single-report cache misses measure and cache reports.
Ordinary cached reads restart retention by refreshing only the entry's expiry metadata.
When space is needed, expired and least-recently-used reports are evicted in batches
before the incoming report is admitted. Bulk comparisons can reuse cached reports
but neither populate the cache nor restart retention. The host's unrelated cache
entries need no size metadata and do not compete with full reports.

With persistent stores, evicted or expired reports are loaded again on demand.
A report larger than the entire budget bypasses the cache; uploads still persist it
and reads still return it. In cache-only hosting, uploads larger than the budget
return HTTP 413 without adding a summary or notifying the data receiver. Increase
the budget or configure persistent storage to accept them.

Cache-only hosting remains supported, but retention is best effort: eviction,
expiration, or a server restart permanently removes report details. An ordinary
read can extend retention but cannot prevent capacity eviction. Summaries may remain
listed after their details expire or are evicted; those links show a missing report.
Use a persistent store when reports must survive these events.

### Writing a persistent store

Implement `IReportStore` and `IRunIndexStore` in `BDTest.Razor.Reports.Storage`.
`IReportStore` reads, overwrites and deletes UTF-8 report streams; reads return null
when missing and transfer stream ownership to the caller. Writes leave the input open.
`IRunIndexStore` upserts `RunIndexEntry` metadata and returns `RunIndexPage` results
for `RunIndexQuery`: 1–500 entries, newest start time first, ordinal ID tie-breaking,
continuation tokens, and optional exact branch/environment/tag filters. Pass cancellation
to every I/O operation. Keep index metadata independent of `TestRunSummary` UI models.

Register both stores with DI before building the host:

```csharp
builder.Services.AddSingleton<IReportStore>(reportStore);
builder.Services.AddSingleton<IRunIndexStore>(runIndexStore);
```

Raw JSON downloads
stream persistent bytes directly; ordinary pages deserialize and cache in the repository.
`IBDTestDataStore` remains supported as a DI registration through `LegacyDataStoreAdapter`
for one migration release, with the old model-loading and uncancellable I/O limitations.
Custom repositories must implement the redesigned `IDataRepository`, including raw reads,
paging and bulk reads; `StoreData` now takes the report ID first.
See [Persistence](docs/docs/report-server/persistence.md) for contracts and a blob-store example,
and [Storage migration](docs/docs/migration/report-storage.md) for migration details.

### Deleting reports

Use `DELETE /bdtest/reports/{id}` (replace `/bdtest` with the configured report prefix and include the host's path base). The endpoint accepts only DELETE and returns 204 No Content when the report was deleted or already absent. The old POST/DELETE `/bdtest/report/delete/{id}` route and its 302 redirect are removed. Invalid IDs return 400; IDs contain 1–128 ASCII letters, digits, dots, hyphens or underscores, excluding `.` and `..`.

Deletion requires the named `AdminAuthorizationPolicy` plus an antiforgery request token and matching cookie. The built-in run list uses JavaScript `fetch`, the configured antiforgery header, and same-origin credentials, then refreshes after 204. Custom controls can generate the URL with `Url.ReportAction("DeleteReport", new { id })` from `BDTest.Razor.Reports.Helpers`. See [Security](docs/docs/report-server/security.md#administrators).

The repository deletes the persistent index before attachment content and report JSON. Index failures leave report data intact; later failures may leave unlisted data for retry. Both cache invalidations are attempted even after failure or cancellation, and the original error propagates as 500. Custom stores must make deletion idempotent. Missing reports are logged at Debug level through the host's `ILogger` services. See [Storage migration](docs/docs/migration/report-storage.md#report-deletion).

### Bulk report comparisons

The Razor report server accepts up to 50 comma-separated IDs for `GET /bdtest/report/test-run-times`
and `POST /bdtest/report/test-run-flakiness` (form field `reportIds`). Each ID must be 1-128 ASCII
letters, digits, dots, hyphens or underscores; `.` and `..` are invalid. Generated report GUIDs
are supported. Invalid lists receive HTTP 400 before any reports are read. An empty selection
redirects to the test-run list. Duplicate IDs count toward the limit but are fetched only once.

Comparisons read at most five reports concurrently. The built-in repository reuses cached reports
without adding or refreshing full-report cache entries. Normal single-report reads still cache
results. Custom `IDataRepository` implementations must implement `GetDataForBulkRead` and should apply
the same cache policy. Every repository operation accepts a cancellation token.

### Custom HTML in reports

`ScenarioHtmlWriter.Text(...)`, both `Link(...)` overloads, and `Image(...)` HTML-encode
supplied text and attribute values. Pass plain text and URLs, without pre-encoding HTML
entities. The delegate overload of `Link` also treats its result as plain text.

Links and images accept HTTP/HTTPS URLs (case-insensitive schemes), protocol-relative
URLs (`//example.com/...`), and relative paths, queries, or fragments. Links also accept
`mailto:` URLs. Other schemes, including `javascript:`, `vbscript:`, `data:`, `file:`,
and `blob:`, are rejected with `ArgumentException`. URLs must be nonempty and cannot
contain control characters, backslashes, or leading/trailing whitespace; null URLs
throw `ArgumentNullException`. Scheme-like colons before the first `/`, `?`, or `#`
must belong to an allowed scheme; use `./name:part.png` for a relative filename with a colon.
This policy restricts schemes, not destination hosts.

Use `ScenarioHtmlWriter.Custom(html)` explicitly when you need raw HTML. It preserves
your markup. The report server continues to isolate custom output in its sandboxed
iframe; helper URL acceptance does not grant navigation or script permissions to that iframe.

### Skipped scenarios and steps

A global skip-test rule skips the entire scenario. A scenario whose steps are all
skipped is also `Skipped`. A mix of executed and intentionally skipped steps is
`Passed` when the executed steps pass; this includes `Debug.ShouldSkipWhenStep`.
A framework skip exception aborts the scenario without retries, records that step
and remaining unexecuted steps as `Skipped`, and preserves previously passed steps.
Genuine inconclusive exceptions remain `Inconclusive`.

NUnit and TUnit adapters propagate whole-scenario skips to their runners. MSTest
uses `Assert.Inconclusive`, which appears as a skipped/not-executed runner result;
BDTest still records a rule-driven or all-steps skip as `Skipped`. An explicit
MSTest `Assert.Inconclusive` in a step remains `Inconclusive` in BDTest.

The xUnit v3 adapter uses `Assert.Skip(reason)` for whole-scenario skips and
registers `Xunit.Sdk.SkipException` for skips raised inside steps. Both BDTest and
the xUnit runner report these scenarios as skipped, without retries.

Standalone `BDTestBase`/`BDTestBuilder` callers can still inspect the returned
skipped scenario. Custom adapters can override `OnScenarioSkipped(string reason)`
to invoke their runner's skip mechanism. Register runner skip exception types in
`BDTestSettings.CustomExceptionSettings.SkipExceptionTypes`, keeping genuine
inconclusive exceptions in `InconclusiveExceptionTypes`.

Run `pwsh scripts/Test-SkipOutcomes.ps1` to verify actual NUnit, xUnit, MSTest and TUnit
runner outcomes as well as BDTest's scenario and step statuses.

### Exception outcomes

Steps, scenarios, and outline examples share the same exception classification.
Types registered in `BDTestOptions` for an explicit run, or in
`BDTestSettings.CustomExceptionSettings` for the legacy default run, match both
the registered type and its subclasses. Explicit runs use their frozen registrations. Classification recursively unwraps
`TargetInvocationException` and `AggregateException` with exactly one inner
exception. An aggregate with multiple inner exceptions (or none) stays intact
and is `Failed` unless its own type or a base type is explicitly registered;
BDTest never chooses one inner exception and hides the others.

After unwrapping, precedence is `NotImplementedException` (including subclasses),
then registered success, skip, and inconclusive types, then `Failed`. BDTest
timeouts always remain `Failed`, even when `TimeoutException` is registered.
Classification does not replace the original exception: propagation and failure
diagnostics preserve wrappers and their inner exceptions.

### Step and scenario timeouts

Import `BDTest.Test` to use the timeout extensions. Async steps can accept a
`CancellationToken` at every `Given`, `When`, `Then`, `And`, and `But` position:

```csharp
var scenario = await Given(token => PrepareAsync(token))
    .WithStepTimeout(TimeSpan.FromSeconds(5))
    .When(token => SendRequestAsync(token))
    .WithStepTimeout(TimeSpan.FromSeconds(10))
    .Then(() => VerifyResponse())
    .WithScenarioTimeout(TimeSpan.FromSeconds(20))
    .BDTestAsync();
```

`WithStepTimeout` applies to the most recently added step. `WithScenarioTimeout`
sets a shared budget for all steps in each execution attempt. The earlier of the
step and remaining scenario limits wins. Timeouts are optional and must be
positive, up to `Int32.MaxValue` milliseconds. Without either limit, execution is
unchanged and token-aware steps receive `CancellationToken.None`. In token-aware overloads, cancellation
arguments are omitted from generated step text and `[StepText]` placeholders.

When a limit expires, BDTest requests cancellation and throws `TimeoutException`
with the limit, its scope, and the active step text. The step and scenario retain
`Status.Failed` for existing reports, with `TimedOut = true`; the step exception
and console summary identify the timeout. Later steps do not run. User exceptions
and cancellation raised before the limit keep their existing classification.
Configured retries start a fresh scenario budget and use new step tokens.

Timed steps run on the thread pool so the limit also bounds synchronous work and
the synchronous portion of async methods. Cancellation is cooperative: synchronous
work or async work that ignores the token may continue after the timeout, including
during a retry. BDTest observes eventual task failures, and late completion cannot
change the recorded step status. Pass the token to cancellable operations and
avoid retries when unfinished work could affect another attempt. The scenario
budget covers elapsed time across steps, including step hooks. Hooks are awaited
outside the execution limits; before-hook time reduces the remaining scenario
budget when the step body starts. If after hooks exhaust the scenario budget,
the active step fails once those hooks finish, including on the final step. Retry
hooks and initial report formatting run before the scenario budget starts.
