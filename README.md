# BDTest

[![NuGet](https://img.shields.io/nuget/v/BDTest.svg)](https://www.nuget.org/packages/BDTest/)
[![Documentation](https://img.shields.io/badge/docs-BDTest-1f8a52)](https://thomhurst.github.io/BDTest/)

BDTest is a code-only behaviour-driven testing and reporting framework for .NET.
Write `Given` / `When` / `Then` scenarios in C#, run them with your test framework,
and share readable HTML reports. No Gherkin files or generated test bodies are needed.

## Install

In a .NET 10 executable test project with implicit usings enabled:

```bash
dotnet add package TUnit
dotnet add package BDTest.TUnit
dotnet add package BDTest.SourceGenerator
```

TUnit is the recommended runner. NUnit, xUnit v3 and MSTest adapters are also
available; see [installation and supported targets](https://thomhurst.github.io/BDTest/docs/getting-started/installation).
These examples follow the upcoming major version on `main`; use matching BDTest
package versions. See the [migration guide](https://thomhurst.github.io/BDTest/docs/migration/next-major).

## Write and run a test

```csharp sample="Readme/Readme.cs" title="Compiled example"
using BDTest.TUnit;
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
            .Then(async () => await Assert.That(Context.Balance).IsEqualTo(75m))
            .BDTestAsync();
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

The [first-test walkthrough](https://thomhurst.github.io/BDTest/docs/getting-started/first-test)
adds reusable steps, readable step text and an HTML report. CI compiles this exact
README example against the current source APIs.

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

- [Writing scenarios](https://thomhurst.github.io/BDTest/docs/writing-tests/scenarios) and [framework adapters](https://thomhurst.github.io/BDTest/docs/frameworks/overview)
- [Assembly configuration](https://thomhurst.github.io/BDTest/docs/configuration/assembly-runs) and [reports](https://thomhurst.github.io/BDTest/docs/reports/overview)
- [Report server setup](https://thomhurst.github.io/BDTest/docs/report-server/setup), [security](https://thomhurst.github.io/BDTest/docs/report-server/security) and [storage](https://thomhurst.github.io/BDTest/docs/report-server/persistence)
- [Major-version migration](https://thomhurst.github.io/BDTest/docs/migration/next-major)
- [Contributing and checking documentation examples](docs/docs/contributing.md) · [Build details](BUILDING.md) · [Issues](https://github.com/thomhurst/BDTest/issues)

[Buy me a coffee](https://www.buymeacoffee.com/tomhurst) if you would like to support BDTest.
