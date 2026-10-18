---
title: Report server overview
description: What the BDTest report server does and how its pieces fit together.
---

# Report server overview

The BDTest report server is an ASP.NET Core app that stores your test reports and shows them in a browser. Test runs upload their results at the end of a run and get back a URL that you can share.

![BDTest report server test run list](/img/report-server-test-runs.png)

It gives you:

- a list of test runs, filterable by status, environment, tag, branch and machine,
- a page per report: summary, scenarios grouped by story, all scenarios, single scenarios, timings, top defects and warnings,
- run comparisons: test times across runs and a flakiness report,
- trend charts of status counts and durations over time,
- links you can send to anyone with access.

## How it fits together

```text
 test run (NUnit / xUnit / MSTest / TUnit)
   └─ BDTestReportServer.SendDataAndGetReportUriAsync(...)
         │  POST /bdtest/data   (X-BDTest-Upload-Key)
         ▼
 your ASP.NET Core app
   ├─ BDTest.Razor.Reports       controllers + views
   ├─ bounded in-memory cache    recent full reports
   └─ IReportStore + IRunIndexStore  optional persistent storage (Blob, SQL, files…)
```

- **`BDTest.Razor.Reports`** is a NuGet package with the controllers and Razor views. You add it to your own ASP.NET Core app. See [Setup](setup.md).
- **Storage** is pluggable. Without a data store, reports live only in memory. See [Persistence](persistence.md).
- **`BDTest.ReportGenerator.RazorServer`** in the repository is a complete sample host that uses Azure Blob Storage and Microsoft Entra ID. It is not published as a package. See [Sample server](sample-server.md).

## Pages and endpoints

All routes are under `/bdtest`.

| Route | Page |
| --- | --- |
| `GET /bdtest/report/test-runs` | Test run list. `/` redirects here. |
| `GET /bdtest/report/trends` | Status count and duration charts across runs. |
| `GET /bdtest/report/{id}/summary` | Report summary. `/bdtest/report/{id}` redirects here. |
| `GET /bdtest/report/{id}/stories` | Scenarios grouped by story, with status and tag filters. |
| `GET /bdtest/report/{id}/all-scenarios` | All scenarios, with status and tag filters and ordering by name, duration, date or status. |
| `GET /bdtest/report/{reportId}/scenario/{scenarioId}` | One scenario. |
| `GET /bdtest/report/{id}/timings` | Test time summary. |
| `GET /bdtest/report/{id}/top-defects` | Exceptions and how often they occurred. |
| `GET /bdtest/report/{id}/warnings` | Tests that were built but not run. |
| `GET /bdtest/report/{id}/raw-json-data` | The report as [JSON](../reports/json-data.md). |
| `GET /bdtest/report/test-run-times?reportIds=a,b` | Per-scenario fastest, slowest and average times across runs. |
| `GET`/`POST /bdtest/report/test-run-flakiness` | Choose runs, then see which scenarios changed outcome. |
| `POST`/`DELETE /bdtest/report/delete/{id}` | Delete a report. Requires an [admin](security.md#administrators). |
| `POST /bdtest/data` | Upload endpoint used by test runs. See [Sending results](sending-results.md). |
| `GET /bdtest/ping` | Anonymous health and warm-up check. |

## Next steps

1. [Create the app](setup.md)
2. [Send results from your tests](sending-results.md)
3. [Add persistent storage](persistence.md)
4. [Secure it](security.md) and [host it](hosting.md)
