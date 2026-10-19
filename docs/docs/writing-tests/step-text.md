---
title: Step text
description: Control how steps read in reports with the source generator, [StepText], WithArgs, WithStepText, argument formatting and custom converters.
---

# Step text

Configure `BDTestOptions` through [assembly configuration](../configuration/assembly-runs.md), or before starting an [explicit run](../advanced/explicit-runs.md). The examples below belong in that configuration.


Every step in a report has a line of text, such as `Given I have an account with a balance of 100`. BDTest builds it from the step type keyword plus either a `[StepText]` template, the humanised method name, or the step's source text.

## Install the source generator

Steps are plain delegates. To know which method a lambda calls, and with which arguments, BDTest uses compile-time metadata from the optional `BDTest.SourceGenerator` package. **Install it in every project that contains steps:**

```xml
<PackageReference Include="BDTest.SourceGenerator" Version="YOUR_BDTEST_VERSION" PrivateAssets="all" />
```

Use the same version as your other BDTest packages. Adapters do not install it for you. See [Source generator](../advanced/source-generator.md) for details.

| Step | With the generator | Without the generator |
| --- | --- | --- |
| `Given(() => Account.CreateAnAccount(100m))` | `Given I have an account with a balance of 100` | `Given () => Account.CreateAnAccount(100m)` |
| `When(Account.CloseTheAccount)` (method group) | `When close the account` | `When close the account` |
| `Then(async () => { await x; await y; })` | source text | source text |

Without generated metadata, BDTest falls back to the lambda's source text (from `CallerArgumentExpression`). Method groups keep their method, so `[StepText]` and humanised names work for them without the generator, but there are no arguments to show.

## Default text from method names

For a simple method call without `[StepText]`, BDTest humanises the method name, lowercases its first letter and appends the argument values separated by spaces:

```csharp title="Illustrative fragment"
public void CreateAnAccount(decimal balance) { ... }
public void the_user_is_logged_in() { ... }

Given(() => CreateAnAccount(100m))      // "Given create an account 100"
    .And(() => the_user_is_logged_in()) // "And the user is logged in"
```

Both PascalCase and underscore-separated names work. BDTest uses these rules:

- If the name contains `_`, split only on underscores and keep each part's casing. Ignore empty parts from repeated, leading or trailing underscores.
- Otherwise, split between lowercase and uppercase letters, between letters and digits in either direction, and before the last capital of an acronym followed by lowercase letters (`MFAToken` becomes `MFA token`).
- Lowercase each word except acronyms of two or more uppercase letters. Underscore-separated parts keep their original casing instead.
- Capitalize the first letter for scenario text. For step text, lowercase that first letter, including when it starts an acronym: `MFAToken` becomes `mFA token`.
- Use invariant casing. Names containing characters other than letters, digits and underscores, such as `<RunAsync>d__12`, stay unchanged before the step's first-letter conversion. Names containing only underscores also stay unchanged. `MoveNext` becomes `Move next`.

For example, `UserLogsInWithMFA` produces scenario text `User logs in with MFA` and step text `user logs in with MFA`. `Order2IsShipped` becomes `Order 2 is shipped` / `order 2 is shipped`. `When_HTTPRequest_arrives` becomes `When HTTPRequest arrives` / `when HTTPRequest arrives`.

The next major version removes the Humanizer dependency. See [generated name text changes](../migration/generated-name-text.md) for release notes and migration examples.

## `[StepText]`

Use `[StepText]` on the step method to write the text yourself. `{0}`, `{1}` and so on are replaced with the argument values, using `string.Format` placeholders:

```csharp title="Illustrative fragment"
[StepText("my name is {0} {1}")]
public void SetName(string firstName, string lastName) { ... }

await Given(() => SetName("Tom", "Longhurst"))  // "Given my name is Tom Longhurst"
    .When(() => SetName("Tom", "Longhurst"))    // "When my name is Tom Longhurst"
    .Then(() => SetName("Tom", "Longhurst"))    // "Then my name is Tom Longhurst"
    .And(() => SetName("Tom", "Longhurst"))     // "And my name is Tom Longhurst"
    .BDTestAsync();
```

### Parameter overrides

Replace a placeholder with fixed text, for example to hide a secret. Each override has the form `"index:replacement"`:

```csharp title="Illustrative fragment"
[StepText("I sign in as {0} with password {1}", "1:********")]
public Task SignIn(string user, string password) { ... }

[StepText("I transfer {0} from {1} to {2}", new[] { "1:my current account", "2:my savings account" })]
public Task Transfer(decimal amount, string from, string to) { ... }
```

An override that does not start with an index and a colon throws `InvalidStepTextParameterException`.

## Which arguments are captured automatically

The generator captures arguments when the lambda's body is a single method call and each argument is:

- a compile-time constant (except enum constants), or
- a field or property path rooted in `this` or a static member, such as `Context.OrderId` or `Current.Order.Id`. Private members are fine.

```csharp title="Illustrative fragment"
Given(() => Submit(Context.OrderId)); // read just before the step runs
When(() => Check(42, "ready"));       // constants
Then(() => Check(Current.Order.Id));  // static property, read just before the step runs
```

Everything else needs explicit arguments: captured local variables and parameters, method calls, indexers, operators, `new` expressions, enum constants, expanded `params`, omitted optional arguments, `ref`/`out`/`in` arguments and user-defined conversions. Statement bodies, `await` bodies, null-conditionals, patterns, extension methods and local functions keep their source text.

## `WithArgs`

Call `.WithArgs(...)` after a step to supply argument readers, in the method's parameter order:

```csharp title="Illustrative fragment"
var order = GetOrder();

await When(() => Orders.Submit(order))
    .WithArgs(() => order)
    .Then(() => Orders.TheOrderIsAccepted(Context.OrderId))
    .BDTestAsync();
```

- Explicit readers replace automatic ones.
- For cancellation-aware steps, leave out `CancellationToken` parameters. Supplying the full list also works; token readers are never evaluated.
- When the step has method metadata, `WithArgs` restores the `[StepText]` template or humanised name. Without metadata, the values are appended to the source text.

## When arguments are read

Generated and explicit argument readers run **once per executed attempt**, immediately before the step runs: after before-step hooks and skip checks.

- [Retries](retries.md) read current state again, from the recreated `Context`.
- Building the chain, reporting, skipped steps and unreached steps never evaluate readers. Unreached steps keep placeholders or source text.
- The step delegate still evaluates its own arguments normally, so keep argument expressions free of side effects.
- If a reader or converter throws, the text shows `<error: ExceptionType>` and the step gets a diagnostic. It never fails an otherwise successful step.

## `WithStepText`

Call `.WithStepText(() => "...")` after a step to replace its text in that scenario only. Use it for a custom sentence, or when arguments do not convert to readable text:

```csharp title="Illustrative fragment"
await When(() => UpdateSteps.UpdateTheField(request => request.EmailAddress, newEmailAddress))
    .WithStepText(() => $"I update the customer's email address to '{newEmailAddress}'")
    .Then(() => HttpAssertions.TheStatusCodeIs(HttpStatusCode.OK))
    .BDTestAsync();
```

The delegate runs when the step executes, and again on each retry, so it can show values changed by earlier steps. It also runs when BDTest formats a step that was skipped or not reached. It is never called while the chain is built. Keep it free of side effects. A `null` callback, or a callback returning `null`, empty or whitespace text, uses the automatic text for that attempt. `WithArgs` readers can return `null`; it is rendered as `null`.

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
| Reading throws | `<error: ExceptionType>` |

A registered [custom converter](#custom-converters) takes precedence over all of these.

## Custom converters

Implement `IStepTextStringConverter<T>` to control how a type appears in step text:

```csharp title="Illustrative fragment"
using BDTest.Interfaces;

public sealed class PersonStringConverter : IStepTextStringConverter<Person>
{
    public string ConvertToString(Person person) => $"{person.FirstName} {person.LastName}";
}
```

Register it in `IBDTestConfiguration.Configure(BDTestOptions options)`, or before starting a standalone run:

```csharp title="Illustrative fragment"
options.RegisterCustomStringConverter<Person>(new PersonStringConverter());
```

- A converter for `T` applies to values assignable to `T`, including derived classes and interface implementations.
- The first matching registration wins. The registered generic `T` selects the interface contract, including explicit implementations; BDTest does not infer a different contract from the converter's runtime type.
- Registrations are copied when the run starts. Later option changes affect only later runs; retries use the same snapshot.
- Converter callbacks may run concurrently and must protect mutable state. Null converters are rejected.
- `StepTextHelper.GetStepText(method, arguments, run: run)` uses that run's converters. Omitting `run` uses default value formatting and no custom converters.

## Scenario outline placeholders

In a [scenario outline](scenario-outlines.md), `<Name>` placeholders in step text are replaced with the example row's property values.

## Tables and doc strings

Use `StepTable` and `StepDocString` from `BDTest.Test` for structured step arguments. Both the hosted Razor report and the standalone HTML report show tables and multiline code blocks with HTML-encoded values.

```csharp title="Illustrative fragment"
using BDTest.Test;

private StepTable Users => StepTable.From(new[]
{
    new { Name = "Alice", Age = 30 },
    new { Name = "Bob", Age = 40 }
});

private StepDocString Payload => new StepDocString("{\n  \"active\": true\n}", "application/json");

// With source-generated readers for these member properties:
await Given(() => TheseUsersExist(Users))
    .When(() => Post(Payload))
    .Then(CheckResponse);
```

`StepTable.From<T>(rows)` snapshots public readable instance properties in ordinal name order, excluding indexers. `StepTable.From(new[] { "Name", "Age" }, new object[] { "Alice", 30 })` supplies explicit columns and rows. Every row must match the header count. Cells use invariant formatting; null cells and headers become empty strings. Headers and rows are immutable snapshots, so later changes to the input do not alter the report. Create a new table to capture changed values.

Automatic capture follows the [generator's argument rules](#which-arguments-are-captured-automatically). Use a member property as above, or `.WithArgs(() => table)` for a captured local or factory expression. BDTest does not evaluate arbitrary factory calls from lambda source text. Structured arguments bypass custom string converters: inline text receives `(table: 2 rows)`, `(application/json)` or `(doc string)`, and the full value is stored in `Step.Arguments`.

To keep an existing step method signature, attach report values to the most recent step:

```csharp title="Illustrative fragment"
await Given(() => TheseUsersExist(users))
    .WithTable(() => StepTable.From(users))
    .When(() => Post(json))
    .WithDocString(() => new StepDocString(json, "application/json"))
    .Then(CheckResponse);
```

`WithTable` and `WithDocString` also accept an already constructed value. Multiple attachments are allowed. Lazy readers run after before-step hooks and skip checks, once per executed attempt. They are never called while building, inspecting or reporting an unexecuted chain. Retries refresh captured arguments; outline rows and sibling chains keep independent results. Reader failures become step diagnostics without failing the step.

A nonempty `WithStepText` override still bypasses inline argument readers, including automatic structured arguments. Use explicit `WithTable` or `WithDocString` attachments with an override; those readers still run. Outline placeholders affect inline text only; structured content is preserved verbatim.

Tables initially display 20 rows. **Show all** expands the remaining rows without discarding data. Doc strings preserve whitespace and carry an encoded `data-content-type` hint on the code element; the report does not load a syntax-highlighting service. Structured arguments are included in [JSON exports](../reports/json-data.md) and report uploads.
