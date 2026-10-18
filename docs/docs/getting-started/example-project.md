---
title: Example project
description: A tour of the BDTest.Example sample project in the repository.
---

# Example project

The repository contains a small sample in [`BDTest.Example`](https://github.com/thomhurst/BDTest/tree/main/BDTest.Example). It shows a typical layout for an API acceptance-test suite. The sample uses NUnit; the structure is the same with TUnit (`TUnitBDTestBase<TContext>` and TUnit hooks).

| File | What it shows |
| --- | --- |
| `MyTestBase.cs` | A shared base class deriving from `NUnitBDTestBase<MyTestContext>`. Step classes are exposed as getter properties: `Steps => new(Context)`. |
| `Context/MyTestContext.cs`, `Context/MyApiContext.cs` | A context object graph that wraps an `HttpClient`, the request and the response. |
| `Steps/MyStepClass.cs` | Action steps that take the context in their constructor. |
| `Steps/Assertions/MyAssertionStepClass.cs` | Assertion steps, for example `[StepText("the HTTP status code is {0}")]`. |
| `ExampleTest.cs` | `[Story]`, `[ScenarioText]`, a `When(...).Then(...)` chain and a custom `[BugInformation]` attribute. |
| `BugInformationAttribute.cs` | A `TestInformationAttribute` subclass that adds a bug link to the report. |
| `DifferentReportScenarioTests.cs` | Passed, failed, ignored and inconclusive outcomes, setup and teardown output, `[StepText]` on private methods, and `ScenarioHtmlWriter.Link(...)`. |
| `SetUpFixture.cs`, `HtmlReportGenerationExample.cs` | Writing JSON and HTML reports in `[OneTimeTearDown]`. |

## Run the sample

```bash
git clone https://github.com/thomhurst/BDTest.git
cd BDTest
dotnet test BDTest.Example/BDTest.Example.csproj
```

Some example scenarios fail, skip or end inconclusive on purpose, to show how each outcome appears in a report. The example's teardown writes a JSON and an HTML report to a temporary folder and deletes it afterwards; copy `HtmlReportGenerationExample.GenerateReports(...)` with your own output directory to keep the files.

## Suggested layout for your own suite

```text
MyApp.AcceptanceTests/
├── Context/
│   └── MyTestContext.cs          # per-test state
├── Steps/
│   ├── AccountSteps.cs           # actions
│   └── Assertions/
│       └── AccountAssertions.cs  # checks
├── Attributes/
│   └── BugInformationAttribute.cs
├── MyTestBase.cs                 # adapter base class + step properties
├── ReportSetup.cs                # one-time report upload / file output
└── Features/
    └── AccountTests.cs           # scenarios grouped by story
```
