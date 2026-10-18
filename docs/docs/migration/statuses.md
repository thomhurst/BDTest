---
title: Status changes
description: The NotRun and Pending statuses, the new meaning of Inconclusive, and changed numeric enum values.
---

# Status changes

The next major version adds two statuses and changes how unreached steps are reported.

## What changed

- New scenarios and steps start as `Status.NotRun`, which is also `default(Status)`.
- Running scenarios and steps are `Status.Pending`. Before-step hooks see `Pending`.
- Steps after a failure stay `NotRun`. Previously they were `Inconclusive`.
- Whole-scenario skips still mark steps `Skipped`.
- Retries reset the scenario and its steps to `NotRun` before retry hooks run.
- `Inconclusive` now only means that a registered inconclusive exception occurred.

## What to update

- Replace checks that used `Inconclusive` to detect unreached steps with `NotRun`.
- Update status `switch` statements and report consumers to handle `NotRun` and `Pending`.
- HTML and report server pages show "Not run" and "Pending" separately from inconclusive results.

`GetOrder()` now ranks statuses as `Failed`, `NotImplemented`, `Inconclusive`, `Skipped`, `NotRun`, `Pending`, `Passed`. In mixed report summaries, passes still override skips, while unfinished results take precedence over passes.

## Stored values

The numeric enum values changed. Store status **names**, not integers.

- BDTest writes statuses as names, including in run summaries.
- Its readers keep historical named statuses, including ambiguous `Inconclusive` results.
- Legacy integers `0`–`4` are read as `Passed`, `Failed`, `Inconclusive`, `NotImplemented` and `Skipped`. Integer tokens are always interpreted with this legacy mapping; never write the new numeric values to JSON.

See [Statuses](../configuration/statuses.md) for the current model.
