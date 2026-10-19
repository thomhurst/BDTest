---
title: Retries
description: Retry failing scenarios with [BDTestRetry] or run retry rules, and reset state between attempts.
---

# Retries

Configure `BDTestOptions` through [assembly configuration](../configuration/assembly-runs.md), or before starting an [explicit run](../advanced/explicit-runs.md). The examples below belong in that configuration.


BDTest can retry a scenario that fails. Retries happen inside one execution: the report keeps one entry per scenario, together with the history of every attempt.

Only failures are retried. Passed, skipped and inconclusive results, and exceptions registered as success, are never retried.

## `[BDTestRetry]`

Apply `[BDTestRetry(count)]` to a test method, a test class or the whole assembly. `count` is the number of retries after the first attempt.

```csharp
[Test]
[BDTestRetry(2)] // up to 3 attempts in total
public async Task CallsAFlakyService()
{
    await When(() => Api.CallTheService())
        .Then(() => Assertions.TheCallSucceeded())
        .BDTestAsync();
}
```

```csharp
[assembly: BDTest.Attributes.BDTestRetry(1)]
```

BDTest looks for the attribute on the scenario method first, then on the step frames, the story class, the calling frame and finally the assembly. The closest attribute wins, so `[BDTestRetry(0)]` on a method turns off a class or assembly default.

## Run retry rules

Retry based on the exception, for example for known transient errors:

```csharp
options.RetryTestRules.Add(
    exception => exception is HttpRequestException,
    retryLimit: 3);
```

When several rules match, the largest `retryLimit` applies. A scenario that a rule no longer allows to retry can still use a `[BDTestRetry]` attribute.

## What happens between attempts

Before each retry, BDTest:

1. resets the scenario and its steps to `NotRun`,
2. runs methods on the test class marked `[BDTestRetryTearDown]`,
3. calls the test class's `OnBeforeRetry()` override,
4. disposes the previous owned context, creates and initializes a new `Context` (and runs [`ConfigureContext`](test-context.md#configuring-a-new-context) again),
5. runs methods marked `[BDTestRetrySetUp]`,
6. runs the steps again.

```csharp
public class OrderTests : TUnitBDTestBase<OrderContext>
{
    [BDTestRetryTearDown]
    public async Task CleanUpPartialOrder() => await Orders.DeleteAsync(Context.OrderId);

    [BDTestRetrySetUp]
    public void RecreateClient() => Context.Client = new HttpClient();

    public override Task OnBeforeRetry()
    {
        Console.WriteLine("Retrying...");
        return Task.CompletedTask;
    }
}
```

An exception in any retry hook fails the scenario and ends retrying, even if the original exception would otherwise have a custom outcome. BDTest throws `ErrorOccurredDuringRetryActionException` with the hook's exception as `InnerException`, and records a failed attempt in the retry history. See [Retry hook errors](../configuration/retry-hook-errors.md). [Step hooks](step-hooks.md) run again on every attempt. Each attempt also gets a fresh [scenario timeout](timeouts.md) budget.

## Retry history

| Member | Description |
| --- | --- |
| `Scenario.RetryCount` | Number of retries performed. |
| `Scenario.Attempts` | Every completed attempt in order, including the first and the last. |
| `Step.IsRetry` | Whether the step ran as part of a retry. |

Each `ScenarioAttempt` has a one-based `Number`, `Status`, `Exception` (type, message and stack trace), `StartTime`, `EndTime` and `TimeTaken`. `Scenario.Status`, `Steps` and `TimeTaken` describe the final attempt.

JSON reports include the history, and older reports load with an empty history. Both the HTML report and the report server show a **Retry history** section for retried scenarios, so a failure stays visible after a passing retry.

With NUnit, each attempt's steps and step hooks run with isolated assertion state through NUnit's public exception constraint API. A final passing attempt lets NUnit report a pass, including after `Assert.That` or `Assert.Multiple` failed earlier. A failure outside the scenario still fails the test; warnings from scenario attempts remain visible. If the final attempt throws after recording other assertion failures, NUnit reports both; earlier attempts discard those assertion diagnostics. Retry conditions receive the original exception. No teardown result override is used. TUnit's own `[Retry]` attribute retries the whole test method instead; use either, not both.
