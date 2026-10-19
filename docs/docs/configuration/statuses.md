---
title: Statuses
description: The Status values for steps and scenarios, how a scenario's status is decided, and how statuses are ranked in reports.
---

# Statuses

Steps, scenarios, outline rows and whole test runs all use `BDTest.Contracts.Status`.

| Status | Meaning |
| --- | --- |
| `NotRun` | Not executed. This is the default value. New steps start here, and steps after a failure stay here. |
| `Pending` | Currently running. Before-step hooks see this status. |
| `Passed` | Completed successfully, or a registered success exception was thrown. |
| `Failed` | An exception was thrown, or a timeout expired. |
| `Inconclusive` | A registered inconclusive exception was thrown. |
| `NotImplemented` | A `NotImplementedException` was thrown. |
| `Skipped` | Intentionally not executed: a skip rule, a skip exception or a debug skip. |

## Scenario status

| Situation | Scenario status |
| --- | --- |
| Every step passed | `Passed` |
| Some steps intentionally skipped, the rest passed | `Passed` |
| Every step skipped, or a global skip-test rule matched | `Skipped` |
| A step threw | The status of the [exception](exceptions.md) |
| Retrying | Reset to `NotRun` before retry hooks run |

## Ranking

`status.GetOrder()` ranks statuses from most to least severe:

1. `Failed`
2. `NotImplemented`
3. `Inconclusive`
4. `Skipped`
5. `NotRun`
6. `Pending`
7. `Passed`

Report summaries use this ranking to pick an overall status. Passes override skips in mixed summaries, while unfinished results (`NotRun`, `Pending`) take precedence over passes.

## Storing statuses

Statuses are serialized as names, for example `"Passed"`. Store names rather than integers: the numeric values changed in the latest major version. See [Status changes](../migration/statuses.md).
