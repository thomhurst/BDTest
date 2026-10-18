---
title: Scenario outlines
description: Run one scenario over a table of typed example rows.
---

# Scenario outlines

A scenario outline runs the same scenario once per example row. It is the BDTest equivalent of a Gherkin `Scenario Outline` with an `Examples` table, written with typed C# objects.

```csharp
[Test]
public async Task AddingNumbers()
{
    await ScenarioOutline("Adding numbers", new[]
    {
        new { Left = 1, Right = 2, Expected = 3 },
        new { Left = 4, Right = 5, Expected = 9 },
    }, row => Given(() => Calculator.SetNumbers(row.Left, row.Right))
        .WithStepText(() => "numbers <Left> and <Right>")
        .When(() => Calculator.AddNumbers())
        .Then(() => Calculator.TheResultIs(row.Expected))
        .WithArgs(() => row.Expected));
}
```

`ScenarioOutline(scenarioText, examples, factory)` takes:

| Parameter | Description |
| --- | --- |
| `scenarioText` | The outline's scenario name in reports. Placeholders apply to step text, not to this name. |
| `examples` | Any `IEnumerable<TExample>`: anonymous objects, records or classes. At least one row is required. |
| `factory` | A function that returns a new runnable chain for one row. |

Await the outline directly or call `BDTestAsync()`. Like a normal chain, an outline runs once.

## Example tables and placeholders

Public property names of the example type become the columns of the example table. `<Name>` placeholders in step text, including `[StepText]` and `.WithStepText(...)`, are replaced with that row's values:

```csharp
[StepText("I log in as <Name> with role <Role>")]
public Task LogIn(string name, string role) { ... }

public record Login(string Name, string Role);

await ScenarioOutline("Users can sign in", new[]
{
    new Login("Alice", "Admin"),
    new Login("Bob", "Reader"),
}, row => When(() => Users.LogIn(row.Name, row.Role))
    .WithArgs(() => row.Name, () => row.Role)
    .Then(() => Users.TheDashboardIsShown()));
```

`row` is a lambda parameter, so the [source generator](step-text.md#which-arguments-are-captured-automatically) cannot read `row.Name` automatically. Add [`.WithArgs(...)`](step-text.md#withargs) to steps that take row values; without it, the step shows its source text instead of the `[StepText]` template.

- Values are formatted with the invariant culture and captured when the outline is built.
- Unknown placeholders stay unchanged.
- `null` values display as `null`.

## How rows run

- Rows run one after another, each with its own steps, output, status and retry count.
- Each row starts with a fresh `Context`, and [`ContextAmendment`](test-context.md#amending-a-new-context) runs for it. Your framework's setup and teardown run once for the whole outline, so put per-row initialisation in `Given` steps.
- While the factory runs and the context is amended, `BDTestContext.CurrentScenario` is `null` because the row's scenario does not exist yet. Steps and retry amendments see the executing row.
- The factory must return a new chain from the same test instance for each row.

## Results

All rows run, even when one fails. Afterwards the first exception for the most severe outcome is rethrown to the test runner:

- Failure, not-implemented and inconclusive rows take priority, in that order.
- Passed rows mixed with skipped rows pass.
- If every row is skipped, the outline is skipped.

JSON, HTML and report server output contain **one** outline with an example table and per-row results. `BDTestUtil.GetScenarios()` returns the outline scenario: `IsOutline` is `true`, and its `Examples` collection holds the row scenarios with their `ExampleValues`. Each row has an `OutlineId` that points back to its parent.
