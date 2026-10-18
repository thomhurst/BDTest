---
title: Step text
description: Control how steps read in reports with [StepText], WithStepText, argument formatting and custom converters.
---

# Step text

Every step in a report has a line of text, such as `Given I have an account with a balance of 100`. BDTest builds it from the step type keyword and either a `[StepText]` attribute or the method name.

## Default text from method names

Without an attribute, BDTest humanises the method name, lowercases its first letter and appends the argument values separated by spaces:

```csharp
public void CreateAnAccount(decimal balance) { ... }
public void the_user_is_logged_in() { ... }

Given(() => CreateAnAccount(100m))   // "Given create an account 100"
    .And(() => the_user_is_logged_in()) // "And the user is logged in"
```

Both PascalCase and underscore-separated names work.

## `[StepText]`

Use `[StepText]` on the step method to write the text yourself. `{0}`, `{1}` and so on are replaced with the argument values, using `string.Format` placeholders:

```csharp
[StepText("my name is {0} {1}")]
public void SetName(string firstName, string lastName) { ... }

await Given(() => SetName("Tom", "Longhurst"))  // "Given my name is Tom Longhurst"
    .When(() => SetName("Tom", "Longhurst"))    // "When my name is Tom Longhurst"
    .Then(() => SetName("Tom", "Longhurst"))    // "Then my name is Tom Longhurst"
    .And(() => SetName("Tom", "Longhurst"))     // "And my name is Tom Longhurst"
    .BDTestAsync();
```

If the template has more placeholders than the method has arguments, building the step throws an `ArgumentException` that shows the template and the arguments.

### Parameter overrides

Replace a placeholder with fixed text, for example to hide a secret. Each override has the form `"index:replacement"`:

```csharp
[StepText("I sign in as {0} with password {1}", "1:********")]
public Task SignIn(string user, string password) { ... }

[StepText("I transfer {0} from {1} to {2}", new[] { "1:my current account", "2:my savings account" })]
public Task Transfer(decimal amount, string from, string to) { ... }
```

An override that does not start with an index and a colon throws `InvalidStepTextParameterException`.

## `WithStepText`

Call `.WithStepText(() => "...")` straight after a step to replace its text in that scenario only. This helps when the arguments do not convert to readable text, for example a `Func<>` or an expression:

```csharp
await When(() => UpdateSteps.UpdateTheField(request => request.EmailAddress, newEmailAddress))
    .WithStepText(() => $"I update the customer's email address to '{newEmailAddress}'")
    .Then(() => HttpAssertions.TheStatusCodeIs(HttpStatusCode.OK))
    .BDTestAsync();
```

The delegate runs when the step executes, so it can show values that change during the scenario. If it returns an empty string, BDTest uses the automatic text.

## How arguments are formatted

| Argument value | Text |
| --- | --- |
| `null` | `null` |
| `string` | the string |
| Delegate | `ToString()` of the delegate. It is never invoked. |
| `IDictionary` | `key=value` pairs joined by `,` |
| Other `IEnumerable` | elements joined by `, ` |
| `CancellationToken` | left out of the text and of `[StepText]` placeholders |
| Anything else | `ToString()` |
| Evaluation throws | `<error: ExceptionType>` |

A registered [custom converter](#custom-converters) takes precedence over all of these.

:::info Arguments are evaluated when the step is added
Automatic step text captures argument values when the step is added to the builder. BDTest reuses that text during execution, retries, reporting and continuations. Argument expressions are evaluated once for this text and again when the step runs, so keep them free of side effects. To show a value that is only known later, use `WithStepText`.
:::

## Custom converters

Implement `IStepTextStringConverter<T>` to control how a type appears in step text:

```csharp
using BDTest.Interfaces;

public sealed class PersonStringConverter : IStepTextStringConverter<Person>
{
    public string ConvertToString(Person person) => $"{person.FirstName} {person.LastName}";
}
```

Register it during test-run setup:

```csharp
BDTestSettings.RegisterCustomStringConverter(new PersonStringConverter());
```

Rules:

- A converter for `T` applies to values assignable to `T`, including derived classes and interface implementations.
- Converters are checked in registration order. The first converter that matches wins. Within one converter that implements several `IStepTextStringConverter<>` interfaces, an exact type match wins over an assignable match.
- `RemoveCustomStringConverter(converter)` removes the first equal registration and returns whether one was found. Duplicates are allowed. Both methods reject `null`.
- Registration and removal are thread-safe. Each argument uses a snapshot of the converters. Changes do not regenerate text for steps already built.
- Converter callbacks can run concurrently. Protect any mutable state inside a converter.

`BDTestSettings.CustomStringConverters` is a legacy `List<object>`. You may use it directly during setup, before tests run concurrently. For concurrent access, hold `lock (BDTestSettings.CustomStringConverters)` for every read and write, or use the registration methods instead.

## Scenario outline placeholders

In a [scenario outline](scenario-outlines.md), `<Name>` placeholders in step text are replaced with the example row's property values.
