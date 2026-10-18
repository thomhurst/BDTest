---
title: JSON serialization changes
description: BDTest moved from Newtonsoft.Json to source-generated System.Text.Json. What changes for report readers, data stores and report hosts.
---

# JSON serialization changes

`BDTest` and `BDTest.Razor.Reports` use source-generated `System.Text.Json` and no longer depend on Newtonsoft.Json. The core package still targets `netstandard2.0` and references the `System.Text.Json` package.

## Reading and writing reports

Use `BDTestJsonHelper.Serialize(report)`, `BDTestJsonHelper.Deserialize(json)` and `BDTestJsonHelper.DeserializeAsync(stream)` from `BDTest.Helpers`. See [JSON data](../reports/json-data.md).

- Both readers accept historical Newtonsoft reports, including `$id`/`$ref` references and `$values` collection wrappers. They reject missing, duplicate and cyclic references.
- Both readers limit expanded legacy JSON to 100 MiB by default, including reads from persistent storage. Pass `maximumExpandedSize` for a different budget. The upload endpoint keeps its 10 MiB limit.
- The async reader accepts a UTF-8 stream and leaves it open.

## The JSON shape

- Writers emit plain JSON. Shared steps, metadata and example values are written as independent values. Do not depend on object identity after deserializing. Outline rows still identify their parent through `OutlineId`.
- Property names are unchanged, including lowercase `id` at the report root.
- Statuses and step types are written as enum names. Historical numeric values remain readable.
- Step, scenario and attempt `TimeTaken` values keep the `d.hh:mm:ss:fffffff` format. Nullable timeouts and the timer's `ElapsedTime` keep the standard invariant `TimeSpan` format.

## Generated metadata

`BDTestJsonContext` (in `BDTest.Helpers`) provides metadata for report types. `ReportJsonContext` (in `BDTest.Razor.Reports.Helpers`) provides metadata for stored run summaries such as `TestRunSummary`. Use their `JsonTypeInfo` properties when calling `System.Text.Json` directly, for example in a custom [data store](../report-server/persistence.md). Historical reports with references must go through the BDTest readers.

Serialization does not use global Newtonsoft settings or your host's MVC JSON options. BDTest's report writer has a fixed wire contract.

## Report hosts

`AddBDTestReports()` registers BDTest's controllers and services without replacing MVC input or output formatters or changing host JSON options. Existing historical reports still render through the report server and Azure storage.

- If your own controllers relied on BDTest enabling Newtonsoft, reference `Microsoft.AspNetCore.Mvc.NewtonsoftJson` and call `AddNewtonsoftJson()` yourself.
- If your app used Newtonsoft through BDTest's transitive dependency, add your own package reference.

## Custom converters

BDTest's JSON converter classes now derive from `System.Text.Json` converters. Replace custom Newtonsoft serialization code and attributes with `System.Text.Json` equivalents, and supply extra application types through your own generated context.

## Trimming and NativeAOT

The report serializer works with `System.Text.Json` reflection disabled. This does not make the whole test engine trimming- or NativeAOT-safe: automatic step text generation still uses reflection and produces IL2075 trimming warnings in `StepTextHelper.GetExpressionValue`.
