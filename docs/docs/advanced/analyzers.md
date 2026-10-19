---
title: Analyzers and code fixes
description: Catch unexecuted scenarios, invalid step text, incomplete stories and discarded asynchronous work at compile time.
---

# Analyzers and code fixes

Install `BDTest.Analyzers` in each project that defines BDTest tests or steps:

```bash
dotnet add package BDTest.Analyzers
```

Keep the package version aligned with your other BDTest packages. Mark the reference private so your test project does not export the tooling to its consumers:

```xml
<PackageReference Include="BDTest.Analyzers" Version="*" PrivateAssets="all" />
```

The package contains a `netstandard2.0` Roslyn component under `analyzers/dotnet/cs`. NuGet loads it automatically; no DLL reference is needed. It requires Roslyn 4.8 or newer (.NET SDK 8 or newer). BDTest step overload selection requires a C# 13 or newer compiler, as described in [Installation](../getting-started/installation.md). The analyzer works alongside the [source generator bundled in BDTest](source-generator.md), which supplies report metadata and also validates named step templates. When the analyzer package is installed, template diagnostics are emitted once by the generator.

| Rule | Default severity | Meaning | Code fix |
| --- | --- | --- | --- |
| [BDT001](#bdt001) | Warning | A scenario chain or execution task is discarded | Await in an existing async method when safe |
| [BDT002](#bdt002) | Error | StepText has invalid placeholders or overrides | Correct the template manually |
| [BDT003](#bdt003) | Warning | Story arguments are missing or conflicting | Complete the attribute manually |
| [BDT004](#bdt004) | Error | An Action step loses asynchronous work | Select a Task step and await its work when safe |

## BDT001

Execute each scenario and observe its completion:

```csharp title="Illustrative fragment"
await Given(() => SetUpAccount())
    .When(() => SubmitOrder())
    .Then(() => AssertOrder())
    .BDTestAsync();
```

Directly awaiting a runnable builder is also valid. Returning `BDTestAsync()` from a Task-returning test is valid. A bare call or `_ = ...` discards the chain or execution task. Discarding `BDTestAsync().ConfigureAwait(false)` is also diagnosed.

The rule proves direct discards, including void expression bodies and async Task/ValueTask expression bodies that omit `await`. It does not track local variables through control flow. Returning a builder, storing it for later, passing it to a helper, and configuring an existing builder do not prove that execution is missing. Those cases are not diagnosed.

Explicit discards in expression-bodied methods and lambdas are diagnosed too:

```csharp title="Illustrative fragment"
// Warning: the chain is discarded without execution.
void DefineScenario() => _ = Given(() => SetUpAccount());

// Warning: the execution task is discarded without awaiting completion.
async Task Test() => _ = Given(() => SetUpAccount())
    .When(() => SubmitOrder())
    .Then(() => AssertOrder())
    .BDTestAsync();
```

This applies when `_` is a discard; assigning to a real variable or parameter named `_` is an ordinary assignment and is not diagnosed. Expression-bodied discards require a manual edit to execute or await the scenario.

The fix adds `await` to a discarded expression statement inside an existing async function that does not return void. It is available only for runnable builders or execution tasks. An incomplete Given chain needs a When and Then before it can execute. The fix never introduces `async void` or changes a method signature.

## BDT002

StepText placeholders use the method's case-sensitive parameter names, with an optional format:

```csharp title="Illustrative fragment"
[StepText("account {name}, balance {balance:N2}", "name=the current account")]
void Account(string name, decimal balance) { }
```

Use `{{` and `}}` for literal braces. Other brace text, such as JSON, malformed placeholders and unmatched braces, remains literal. Overrides use `"parameterName=text"`; replacement text is literal, including any braces or equals signs. Empty replacement text is valid. Positional placeholders (`{0}`) and positional overrides (`"0:text"`) are obsolete and produce errors. Unknown placeholder or override names, null override arrays and malformed overrides are also errors.

`CancellationToken` parameters are always excluded from visible parameter names, including ordinary delegates and methods that have not yet been bound to a step. Referencing a token in a template or override is an error. User formatters, runtime converters and argument values are never executed by compiler validation. The same validation runs at runtime when neither compiler package is installed.

## BDT003

Supply exactly one nonempty role plus both goal and benefit:

```csharp title="Illustrative fragment"
[Story(AsA = "customer", IWant = "to submit an order", SoThat = "I receive my purchase")]
class OrderTests { }
```

Use either `AsA` or `AsAn`. Setting both is a warning. Missing or empty role, `IWant`, or `SoThat` values also produce a warning.

## BDT004

A step bound to `Action` must not be `async void` or discard an awaitable. For example, a statement lambda can accidentally select Action:

```csharp title="Illustrative fragment"
// Error: Action finishes before SaveAsync completes.
Given(() => { SaveAsync(); });

// Valid: BDTest observes completion and failures.
Given(() => SaveAsync());
Given(async () => { await SaveAsync(); });
```

Task, ValueTask, ValueTask of T, and cancellation-aware step overloads are valid. They do not need `AsTask()`. The rule inspects inline Action bodies, including direct calls to async void methods, and async void method groups. It does not follow arbitrary helper implementations or delegate variables, or diagnose storing an awaitable for other code to consume.

For an inline lambda, the fix awaits discarded expressions and selects `Func<Task>` explicitly. Existing control flow is retained. Async void method groups and calls require a manual method-signature change, so no fix is offered. A fix is offered only when the resulting compilation is valid.

## Suppression and severity

Use `.editorconfig` to change a rule for your project:

```ini
[*.cs]
dotnet_diagnostic.BDT001.severity = error
dotnet_diagnostic.BDT003.severity = none
```

For deliberate invalid-input or unexecuted-scenario tests, suppress only the relevant code and explain why:

```csharp title="Illustrative fragment"
#pragma warning disable BDT002 // Verify runtime rejection of an invalid template.
[StepText("{missing}")]
void InvalidTemplate(string onlyParameter) { }
#pragma warning restore BDT002
```

`System.Diagnostics.CodeAnalysis.SuppressMessage` is also supported. Generated code is excluded. Unrelated Given, When, StepText and Story symbols do not trigger these rules.
