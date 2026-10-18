---
title: Generated name text changes
description: Release notes for replacing Humanizer.Core with BDTest's internal method name splitter in the next major version.
---

# Generated name text changes

## Next major release notes

BDTest replaces its `Humanizer.Core` dependency with an internal method name splitter. The core package still targets `netstandard2.0`. There is no new public API or configuration. If your project uses Humanizer APIs received transitively through BDTest, add a direct `Humanizer.Core` package reference.

Generated step and scenario text can change. This is a breaking change for tests, snapshots or dashboards that compare exact report text. `[StepText]`, `[ScenarioText]`, `WithStepText` and `WithScenarioText` overrides keep their supplied text. Update comparisons or supply explicit text to retain a specific title.

The exact [name splitting rules](../writing-tests/step-text.md#default-text-from-method-names) preserve underscore-separated casing and acronyms, split letter/digit boundaries in both directions, and use invariant casing. Scenario text capitalizes the first letter. Step text lowercases only that first letter after splitting, including the first letter of an acronym.

## Differences from Humanizer.Core 3.0.10

These examples show scenario text. Spaces in quoted values are significant.

| Method name | Previous text | New text |
| --- | --- | --- |
| `given_a_user_logs_in` | `given a user logs in` | `Given a user logs in` |
| `when_HTTPRequest_arrives` | `when HTTPRequest arrives` | `When HTTPRequest arrives` |
| `__Given__a_user__` | `"  Given  a user  "` | `Given a user` |
| `Version42beta` | `Version 42beta` | `Version 42 beta` |
| `<RunAsync>d__12` | `<RunAsync>d  12` | `<RunAsync>d__12` |
| `<Run>b__0_1` | `<Run>b  0 1` | `<Run>b__0_1` |
| `___` | `"   "` | `___` |

Names containing punctuation (including compiler-generated names and explicit interface method names) are preserved rather than partly split. `MoveNext` remains `Move next`. Empty or missing scenario names retain the existing missing-text fallback.

Casing no longer depends on the current culture. For example, step method `ItemIsValid` produces `item is valid` even under `tr-TR`, rather than starting with a dotless `ı`.

The regression tests freeze a representative corpus of 29 existing test and sample method names against Humanizer.Core 3.0.10. Every corpus entry retains its output, including `No_Scenario_Text_with_Hyphens`, `NUNitSuccessException`, `MissingRawJsonReportReturns404WithoutHtml`, `ZZZ_EnsureContextsAreUnique` and `Unicode_reports_use_the_exact_UTF8_byte_budget`. The tests also list the intentional differences above without keeping a Humanizer dependency.
