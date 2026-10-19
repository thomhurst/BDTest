---
title: Reporting package
description: Add BDTest.Reporting for execution-side report export and upload in the next major version.
---

# Reporting package

The next major release moves report generation and upload from `BDTest` into
`BDTest.Reporting`. Add it to each project that calls these APIs, at the same
version as your adapter, and rebuild callers:

```bash
dotnet add package BDTest.Reporting
```

The package provides `netstandard2.0` and `net8.0` assets. Adapters depend only on
execution, so reporting is an explicit opt-in. Existing `BDTest` and
`BDTest.Helpers` imports and method signatures remain usable after adding the
reference. Assembly-qualified reflection names must use `BDTest.Reporting`.
There is no binary type forwarder from core: it would create the reverse
execution-to-reporting dependency this major-version change removes.

## API ownership

`BDTest.Reporting` owns `BDTestRunDescriptor`, `BDTestReportServer`,
`BDTestReportUploader`, `BDTestReportUploadOptions`,
`BDTestJsonHelper`, `BDTestHtmlHelper`, `BDTestAttachmentHelper`,
`ExceptionHtmlHelper` and `StepArgumentHtmlHelper`. JSON, HTML and uploads share
one model builder that consumes the selected run's snapshot.

Core owns execution, console output, attachment capture and `HtmlWriter`, which
captures evidence while tests run. `BDTestRun.GetSnapshot()` returns an immutable
`BDTestRunSnapshot`: report ID, execution package version, scenario results,
pending definitions and timer. It validates disposal before copying results and
uses the selected run's clock for an empty run. Captured snapshots survive later
run changes; their attachment metadata does not keep files alive after disposal.
`ExceptionWrapper.ToResult()` and `StepArgument.ToResult()` expose the same
immutable conversion used by execution and the legacy rendering overloads.

Legacy `BDTestJsonContext` remains in core alongside the mutable execution models.
Its generated metadata accesses their internal JSON constructors, setters and
converters without reflection or a new friend grant. Reporting uses that public
metadata for the historical reader and writer. Current report DTOs and their
source-generated serializer remain in the independent `BDTest.Contracts` package.

## Behavior retained

Exports preserve run isolation, report IDs, pending definitions, descriptor
properties, timing, retry attempts, structured arguments and exception trees.
JSON readers retain historical compatibility and reference-expansion limits.
Standalone HTML remains self-contained with its existing parent CSP, sandboxed
custom HTML and event listeners. Upload cancellation, retries, caller-owned
clients and run-owned attachment streams retain their existing behavior.
The configurable uploader retains streaming JSON, optional gzip, upload keys and
per-attempt timeouts; its transport and options move together into reporting.
Keep the run alive until export and upload finish.

`BDTest.Razor.Reports` continues to consume independent contracts. Hosted and
standalone renderer unification is separate work; this package extraction does
not change hosted rendering or upload protocol behavior.
