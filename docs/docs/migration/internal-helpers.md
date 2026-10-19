---
title: Migrating away from implementation helpers
description: Supported replacements for helpers removed from the next major public API.
---

# Migrating away from implementation helpers

The next major version makes the following implementation types internal on both
`netstandard2.0` and `net8.0`. Remove direct references to them when upgrading.

| Former public type | Supported replacement |
| --- | --- |
| `BDTest.Helpers.StepTextHelper` | Build and execute fluent steps. Use `[StepText]`, `WithArgs` and `WithStepText` to control text; read `Scenario.Steps` afterward. Register custom value converters on `BDTestOptions` before starting the run. |
| `BDTest.Helpers.StoryTextHelper` | Use `[Story]` and read `GetStoryText()` on the scenario, test base or context. |
| `BDTest.Helpers.BDTestVersionHelper` | Read `Version` from a report exported with `BDTestJsonHelper`, or inspect the core assembly's `AssemblyInformationalVersionAttribute` when no report is needed. |
| `BDTest.Constants.DefaultValues` (including `ReportServer`) | Read resolved story/scenario text from results. Use `RunFilter` for report query parsing; the historical UI placeholders are handled by the renderer. |
| `BDTest.Output.TimespanConverter`, `BDTest.Output.StatusConverter` | Use `BDTestJsonHelper` for complete reports, or `BDTestJsonContext` for generated JSON metadata. Do not register the implementation converters yourself. |
| `BDTest.Helpers.JsonConverters.ScenarioTextConverter`, `TestInformationAttributeJsonConverter` | Use the same supported report serialization entry points. |

`TypeHelper` and `StoryTextConverter` remain internal. These accessibility changes
do not change stored report JSON: historical durations, status values, story and
scenario strings, information attributes and legacy report defaults remain readable.
`BDTestJsonContext` remains public and its generated metadata uses the internal
converters without requiring reflection-based JSON serialization. For a standalone
`Status` value, use `BDTestJsonContext.Default.Status` (or copy its converter to your
own serializer options); the enum no longer carries an assembly-internal converter
attribute. Report status properties retain their converter attributes, so complete
reports preserve their wire format even with custom context options. See
[JSON migration](json-serialization.md) for the historical format and reader.

The unsigned adapters and external consumers receive no friend access to core.
Custom adapters should continue using the protected `BeginTest`, `EndTestAsync`,
`CaptureStartupOutput`, `BindStartupOutput`, `CompleteStartupOutput`,
`BeginPendingTestExecution` and `ExecuteScenarioStepsAsync` boundary described in
[custom adapters](../frameworks/custom-adapters.md). The fluent builders retain
their concrete return types.

Global `BDTestSettings` and upload overloads without an explicit run were removed
separately. Supply a `BDTestRun` and `BDTestRunDescriptor` as shown in
[explicit run migration](explicit-runs.md); the supported explicit-run upload
methods still accept cancellation tokens. Helper accessibility does not replace
those methods or move report contracts to a new namespace.
