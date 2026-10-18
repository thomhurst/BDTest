---
title: Step hooks
description: Run code before and after every step, globally or per test class, for logging, screenshots and diagnostics.
---

# Step hooks

Step hooks run code around every step. Typical uses are logging, taking screenshots on failure and collecting diagnostics.

## Global hooks

Subscribe to `BDTestSettings.BeforeStep` and `BDTestSettings.AfterStep` during test-run setup. Both are `Func<Step, Task>` events:

```csharp
using BDTest.Settings;
using BDTest.Test;

BDTestSettings.BeforeStep += step =>
{
    Console.WriteLine($"Starting: {step.StepText}");
    return Task.CompletedTask;
};

BDTestSettings.AfterStep += async step =>
{
    if (step.Status == Status.Failed)
    {
        await SaveScreenshotAsync(step.ScenarioId, step.Guid);
    }
};
```

Global handlers can run concurrently for different tests. Keep their shared state thread-safe. Remove a handler with `-=` when you no longer need it.

## Per-test hooks

Override `OnBeforeStep(Step step)` and `OnAfterStep(Step step)` on your test class or base class. They return `Task` and can use the test's `Context`:

```csharp
public abstract class UiTestBase : TUnitBDTestBase<UiContext>
{
    public override async Task OnAfterStep(Step step)
    {
        if (step.Status == Status.Failed)
        {
            // Your own helper that saves a screenshot and returns an http(s) URL.
            var screenshotUrl = await Context.Screenshots.CaptureAndUploadAsync();
            ScenarioHtmlWriter.Image(screenshotUrl);
        }
    }
}
```

## Order

For each step that is reached, BDTest awaits:

1. global `BeforeStep` handlers, in registration order,
2. the test's `OnBeforeStep`,
3. the step,
4. the test's `OnAfterStep`,
5. global `AfterStep` handlers, in registration order.

Subscriptions are captured when the step starts. Changes during a step apply to later steps.

## Exceptions in hooks

- Before hooks stop at the first exception.
- Every after hook still runs if a before hook, the step or another after hook throws.
- The original execution exception takes precedence. Secondary after-hook exceptions are logged.
- Otherwise the first after-hook exception decides the step result, using the usual [exception classification](../configuration/exceptions.md) and [retry](retries.md) rules.

## What after hooks see

After hooks receive the step's `Status`, `Exception`, `Output`, `StartTime`, `EndTime` and `TimeTaken`.

- Timing includes before hooks and the step, and excludes after hooks.
- Output written by hooks is included in the step's output.
- All after hooks observe the execution result before after-hook failures are applied.

## When hooks run

| Situation | Hooks run? |
| --- | --- |
| Normal step | Yes |
| Step skipped by a [skip-step rule](skipping.md) | Yes |
| Each retry attempt | Yes |
| Step not reached because an earlier step failed | No |
| Whole scenario skipped before execution | No |
