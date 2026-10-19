---
title: Step hooks
description: Run code before and after every step, per run or per test class, for logging, screenshots and diagnostics.
---

# Step hooks

Configure `BDTestOptions` through [assembly configuration](../configuration/assembly-runs.md), or before starting an [explicit run](../advanced/explicit-runs.md). The examples below belong in that configuration.


Step hooks run code around every step. Typical uses are logging, taking screenshots on failure and collecting diagnostics.

## Run hooks

Register `IStepHook` instances in `IBDTestConfiguration.Configure(BDTestOptions options)`, or on options before starting a standalone run:

```csharp
options.StepHooks.Add(new StepLogger());

public sealed class StepLogger : BDTest.Interfaces.IStepHook
{
    public Task BeforeStepAsync(BDTest.Test.Step step)
    {
        Console.WriteLine($"Starting: {step.StepText}");
        return Task.CompletedTask;
    }

    public Task AfterStepAsync(BDTest.Test.Step step)
    {
        Console.WriteLine($"Finished: {step.Status}");
        return Task.CompletedTask;
    }
}
```

Hooks can run concurrently for different tests. Keep their shared state thread-safe. Registrations are frozen when the run starts; changing options afterwards affects only later runs.

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

1. run `BeforeStepAsync` hooks, in registration order,
2. the test's `OnBeforeStep`,
3. the step,
4. the test's `OnAfterStep`,
5. run `AfterStepAsync` hooks, in registration order.

Hook registrations remain fixed for the lifetime of the run, including retries.

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
