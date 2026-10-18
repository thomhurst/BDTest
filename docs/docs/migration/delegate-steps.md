---
title: Migrating to delegate steps
description: Step builders now take delegates instead of expression trees. What changes for helpers, step text and arguments.
---

# Migrating to delegate steps

This is a major-version API change. `Given`, `When`, `Then`, `And`, and `But` now accept delegates instead of expression trees. Existing simple step lambdas continue to compile. Helpers returning `Expression<Action>` or `Expression<Func<Task>>` must return `Action` or `Func<Task>` instead. The public expression-based `StepTextHelper.GetStepText` overloads have been removed; use the overload taking a selected `MethodInfo` and lazy argument readers if calling that helper directly.

The core targets `netstandard2.0;net8.0`. Its .NET Standard build supplies internal compiler attribute polyfills and references `System.Threading.Tasks.Extensions` for `ValueTask`; the modern build uses runtime APIs where available. Consumers need C# 13 or newer (.NET SDK 9 or newer) for overload priorities. Ordinary async lambdas select `Func<Task>` or `Func<CancellationToken, Task>`; typed `ValueTask` delegates select their own overloads. `ValueTask<T>` and cancellation-aware `ValueTask<T>` results are awaited and their result values are ignored. Each returned ValueTask is consumed once. No step definition or argument reader is compiled from an expression tree, including on retries and outline rows.

```csharp
Given(() => Initialize())
    .When(async token => await SubmitAsync(token))
    .Then(() =>
    {
        var result = Context.Result;
        Assert.That(result?.Total is > 0);
    });

When(() => SaveValueTaskAsync()); // No AsTask() workaround needed.
```

The cancellation token retains the existing timeout behavior: steps receive a token cancelled when their step or scenario budget expires, and the default token when no timeout applies. Custom awaitables and configured awaitables should be awaited inside an async lambda. An explicitly supplied `Action` has void semantics; BDTest cannot inspect its body for discarded asynchronous results. Avoid `async void` and do not cast asynchronous steps to `Action`.

## Names, attributes, and arguments

Install the optional [BDTest.SourceGenerator package](../advanced/source-generator.md) directly in every project containing step calls. For simple method calls, its metadata preserves humanized names, `[StepText]`, inherited skip attributes (including custom `SkipStepIf` attributes), and supported argument values. Caller file, line, and lambda text identify the call site. Continuation builders preserve these compiler values too. Method groups retain their selected method and attributes without the generator.

Automatic argument capture supports constants and field/property paths rooted in `this` or static state. Captured locals, method calls, allocations, and other unsupported argument expressions need explicit readers:

```csharp
var order = GetOrder();
When(() => Submit(order)).WithArgs(() => order)
    .Then(() => Verify(Context.OrderId));
```

`WithArgs` supplies arguments in method parameter order. For cancellation-aware steps, omit cancellation-token parameters; supplying the full list is also accepted, and token readers are never evaluated. Explicit readers replace automatic readers. Each reader runs once per executed attempt after before-step hooks and skip checks, immediately before invoking the step delegate. The step body still evaluates its arguments normally, so use stable values when getters have side effects. Retries read the recreated Context. Unreached or skipped steps do not run readers. Rendering exceptions become diagnostic text and never fail an otherwise successful step.

Without generated metadata, a lambda's `CallerArgumentExpression` text is the fallback name. Explicit argument values are appended to that text. When method metadata exists but capture is unsupported, adding `WithArgs` restores the method's template and argument formatting. For a custom sentence, use `WithStepText(() => "the expected result is present")`.

Statement bodies, null-conditionals, patterns, local functions, and async/await bodies retain caller text because they do not describe one simple method call. Attributes on methods called inside such bodies are not inferred. Failure output includes the rendered step name, including caller text for fallback delegates.
