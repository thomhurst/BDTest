# BDTest

[![NuGet](https://img.shields.io/nuget/v/BDTest.svg)](https://www.nuget.org/packages/BDTest/)
[![Documentation](https://img.shields.io/badge/docs-BDTest-1f8a52)](https://bdtest.tomlonghurst.dev/)

BDTest is a code-only behaviour-driven testing and reporting framework for .NET.
Write `Given` / `When` / `Then` scenarios in C#, run them with your test framework,
and share readable HTML reports. No Gherkin files or generated test bodies are needed.

Install `BDTest.Reporting` alongside your adapter for JSON export, standalone HTML
and HTTP uploads. It targets `netstandard2.0;net8.0`; use matching BDTest versions
and rebuild consumers when upgrading to the next major release. See
[reporting package migration](https://bdtest.tomlonghurst.dev/docs/migration/reporting-package).

`BDTest.Rendering` provides HTML rendering over immutable contracts without an
execution dependency. Standalone and hosted reports share run overviews and
scenario details, including steps, examples, retries and evidence; tools can
reference it directly for offline HTML. See the
[shared rendering boundary](https://bdtest.tomlonghurst.dev/docs/reports/rendering).

Persistent report storage is available through `BDTest.Reports.Storage.AzureBlob`
and `BDTest.Reports.Storage.FileSystem`; see [report persistence](https://bdtest.tomlonghurst.dev/docs/report-server/persistence).

## Install

In a .NET 10 executable test project with implicit usings enabled:

```bash
dotnet add package TUnit
dotnet add package BDTest.TUnit
```

TUnit is the recommended runner. NUnit, xUnit v3 and MSTest adapters are also
available; see [installation and supported targets](https://bdtest.tomlonghurst.dev/docs/getting-started/installation).
These examples follow the upcoming major version on `main`; use matching BDTest
package versions. See the [migration guide](https://bdtest.tomlonghurst.dev/docs/migration/next-major).

## Write and run a test

```csharp sample="Readme/Readme.cs" title="Compiled example"
using BDTest;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

public class WithdrawalTests : TUnitBDTestBase<AccountContext>
{
    [Test]
    public async Task WithdrawingMoneyReducesTheBalance()
    {
        await Given(() => Context.Balance = 100m)
            .When(() => Context.Balance -= 25m)
            .Then(async () => await Assert.That(Context.Balance).IsEqualTo(75m));
    }
}

public class AccountContext
{
    public decimal Balance { get; set; }
}
```

```bash
dotnet run --project MyApp.AcceptanceTests
```

The [first-test walkthrough](https://bdtest.tomlonghurst.dev/docs/getting-started/first-test)
adds reusable steps, readable step text and an HTML report. CI compiles this exact
README example against the current source APIs.

## Upload reports

`BDTestReportUploader` uploads an explicit run using a caller-owned `HttpClient`.
Configure `BDTestReportUploadOptions` with the report address, upload key and
per-attempt timeout; JSON streams with gzip by default. Environment keys are
loaded only when you explicitly call `BDTestReportUploadOptions.FromEnvironment`.
See [sending results](https://bdtest.tomlonghurst.dev/docs/report-server/sending-results).
Use matching contracts-based host and client package generations for schema-version-1
uploads. Call `UploadReportAsync(run, descriptor, cancellationToken)` for a typed
result with `ReportUrl` and server-assigned `ReportId`, or `UploadAsync` for just
the URL. Legacy server responses remain supported; `ReportId` is null if no
receipt ID is available. Static upload APIs are obsolete forwarders retained for
one release.

## Developing BDTest

Run the core behavior and reporting suite with TUnit:

```bash
dotnet run --project BDTest.Tests
dotnet run --project BDTest.Tests -- --treenode-filter "/*/*/RetryTests/*"
```

NUnit adapter and sample integration coverage stays in `BDTest.NUnit.Tests` and
runs with `dotnet test BDTest.NUnit.Tests/BDTest.NUnit.Tests.csproj`. See
[build details](BUILDING.md) for all suites and validation commands.

## Documentation

- [Writing scenarios](https://bdtest.tomlonghurst.dev/docs/writing-tests/scenarios) and [framework adapters](https://bdtest.tomlonghurst.dev/docs/frameworks/overview)
- [Assembly configuration](https://bdtest.tomlonghurst.dev/docs/configuration/assembly-runs) and [reports](https://bdtest.tomlonghurst.dev/docs/reports/overview)
- [Report server setup](https://bdtest.tomlonghurst.dev/docs/report-server/setup), [security](https://bdtest.tomlonghurst.dev/docs/report-server/security) and [storage](https://bdtest.tomlonghurst.dev/docs/report-server/persistence)
- [Major-version migration](https://bdtest.tomlonghurst.dev/docs/migration/next-major)
- [Contributing and checking documentation examples](docs/docs/contributing.md) · [Build details](BUILDING.md) · [Issues](https://github.com/thomhurst/BDTest/issues)

[Buy me a coffee](https://www.buymeacoffee.com/tomhurst) if you would like to support BDTest.
