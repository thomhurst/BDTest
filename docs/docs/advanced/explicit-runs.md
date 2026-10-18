---
title: Explicit runs
description: Isolate scenario storage and reporting with BDTestRun in custom adapters and long-lived hosts.
---

# Explicit runs

By default, every scenario in a process goes into one shared store, and the report helpers read from it. Custom adapters and long-lived hosts can own a `BDTestRun` instead, to keep a separate set of scenarios and produce a separate report.

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

## Rules

- Override `TestRun` and keep it stable for the test instance. Builders and scenarios capture their run, including builders executed later and scenario outline rows.
- Each run has a new `ReportId` and its own stores for scenarios, unfinished definitions, listeners and output. An explicit run also has its own executor.
- The JSON and HTML helpers and the report upload accept a run, for example `BDTestReportServer.SendDataAndGetReportUriAsync(run, serverAddress, descriptor)`.
- Existing overloads and the framework adapters use the shared default run.

## Lifetime

Dispose a run only after its tests and output writers finish. Disposal clears its stores and prevents new scenarios from being constructed or executed. It does not cancel active tests.

`BDTestUtil.ClearScenarios(run)` clears storage between executions and keeps the same report ID. Start a new run to get a new ID.

## What is still global

`BDTestSettings` configuration is process-wide and applies to every run.
