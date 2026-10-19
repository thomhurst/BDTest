---
title: Compiler step metadata
description: The optional BDTest.SourceGenerator package that records step call-site metadata at compile time.
sidebar_label: Source generator
---

# Compiler step metadata

`BDTest.SourceGenerator` is an optional incremental source generator. It records method identity and lazy argument descriptions for `Given`, `When`, `Then`, `And`, and `But` calls bound to BDTest builders. It targets `netstandard2.0`, uses Roslyn 4.8 APIs, and requires a C# 10 or newer compiler (Roslyn 4.8 or newer; .NET SDK 8 or newer). It does not require preview features, interceptors, or partial test classes. Delegate builders require a C# 13 or newer compiler (.NET SDK 9 or newer) so overload priority selects Task for ordinary async lambdas. The generator itself uses Roslyn 4.8 APIs.

## Installation

Install matching versions of the core and generator packages directly in each project that contains step call sites:

```xml
<PackageReference Include="BDTest" Version="YOUR_BDTEST_VERSION" />
<PackageReference Include="BDTest.SourceGenerator" Version="YOUR_BDTEST_VERSION"
                  PrivateAssets="all" />
```

The generator DLL ships under `analyzers/dotnet/cs`. It has no runtime Roslyn dependency. It is intentionally a separate package; adapters do not install it transitively. The metadata contract lives in both core runtime assets, in `BDTest.Metadata`.

Builders consume this metadata while retaining plain delegates for execution. Method identity supplies humanized names, `[StepText]` and skip attributes. Supported arguments are read immediately before a reached step runs, after hooks and skip checks. Retries read current state again. Unreached steps retain placeholders or caller text and never evaluate readers. Rendering errors appear as `<error: ExceptionType>` plus a step diagnostic; they do not change the step outcome. See [delegate migration](../migration/delegate-steps.md) for explicit argument and fallback behavior.

## Runtime contract for builders

The lookup identity is the consumer assembly plus the exact compiler-supplied `CallerFilePath`, `CallerLineNumber`, and `CallerArgumentExpression("step")` values. Preserve all three optional caller parameters on every builder entry point, including continuation methods. Pass them unchanged to the lookup. Do not use the adapter assembly or the runtime type's assembly as a substitute for the assembly that actually contains the call site. An inherited test method can live in another assembly.

The generator marks the consumer assembly with `StepMetadataProviderAttribute`. `StepMetadataCatalog.Find(callerAssembly, file, line, expression)` returns the call-site description, or `null` when the generator is absent, the call is not recognized, or its identity is ambiguous. It normalizes mapped source locations, including `#line` paths, through the compilation's source resolver. Compiler path mapping therefore matches `CallerFilePath`, including deterministic CI builds.

`StepCallSiteMetadata.ArgumentExpression` always retains the original lambda text. `Method` is null for unsupported bodies. Otherwise `Method.Resolve(callerAssembly)` returns the exact selected `MethodInfo`, including closed generic arguments. `DocumentationId` identifies the declaration for diagnostics. Reflection resolution matches name, generic arity and every parameter type; it never picks an overload by name alone. Type/member descriptors use metadata names, so generated C# never references private or otherwise inaccessible members. Private nested types, closed generic declaring types and closed generic methods are supported. Dynamic parameter and generic argument types resolve as `System.Object`, matching reflection. Method attributes can be read from that `MethodInfo`, including `StepTextAttribute`, its parameter overrides, and custom `SkipStepAttribute` subclasses such as user-defined `SkipStepIf` attributes, using inherited attribute lookup.

Call `TryBindArguments(callerAssembly, originalStepDelegate, out accessors)` while constructing the step definition. This binds reflection member paths, without reading fields or properties, invoking the step, or constructing user attributes. A successful result supplies argument accessors in **parameter order**, with names in `ParameterNames`. Each accessor reads current values when invoked; do not cache its result during construction. A false result requires caller-text/explicit-argument fallback. Method metadata remains usable independently of automatic argument capture.

When the builder does not already know the caller assembly, use `StepMetadataCatalog.Find(file, line, expression, out callerAssembly)`. It searches loaded catalogs for a unique full identity and returns the matching assembly for subsequent resolution and binding. Multiple matches return null, so copied source in different assemblies cannot silently select another method. Catalog providers (including absent providers) are cached with weak assembly keys. A weak assembly snapshot is refreshed after assembly-load notifications, rather than enumerating the AppDomain on each step. Newly loaded assemblies are included on the next lookup. Builders that know the assembly can use the explicit overload to avoid searching other catalogs. This discovery uses reflection and requires the generated catalog and reflected step members to be preserved in trimmed/AOT applications; automatic trimming support is outside this package's contract.

The generator supplies unevaluated metadata; core builders own rendering, retries, diagnostics, string conversion, and cancellation-token omission. Accessors may throw when invoked (for example, a property getter or null receiver); the renderer must handle that explicitly. Do not evaluate them merely to find out whether capture is supported.

## Supported automatic arguments

The lambda must have a single expression body calling an ordinary method. The generator supports compiler constants (except enum constants) and field/non-indexed-property paths rooted in captured `this` or a static member. For example:

```csharp title="Illustrative fragment"
Given(() => Submit(Context.OrderId)); // this.Context.OrderId, evaluated by an accessor later
When(() => Check(42, "ready"));       // constants
Then(() => Check(Current.Order.Id));  // a static Current property is read later
```

Fields and properties may be private. Narrow integral constants retain their runtime types. Base-qualified member reads and paths through nullable values (such as `Optional.Value`) use fallback because reflection cannot preserve their access semantics. A nullable value at the end of a path remains supported. Getters are not executed while generating, looking up, or binding metadata. Named arguments are reordered into parameter order. Identity/reference conversions and implicit boxing are supported; other nonconstant conversions require explicit arguments. A cancellation parameter forwarded directly from the step lambda is represented without reading it and omitted from step text.

Automatic capture deliberately does **not** inspect compiler-generated closure fields. Captured locals and parameters, method calls inside arguments, indexers, operators, allocations, enum constants, expanded `params`, omitted optional arguments, `ref`/`out`/`in` arguments, and user-defined conversions use explicit argument fallback. A compiler may place `this` into a shared display class when another lambda captures locals; binding then fails safely, even if the individual argument path is otherwise supported. Use explicit `.WithArgs(...)` accessors for these cases.

Open generic declarations whose selected declaring type or method type arguments still contain type parameters do not have a closed runtime identity and use caller-text fallback. Closed generic arguments currently must be named types, not array/pointer/function-pointer type arguments. Extension methods and local functions also use caller-text fallback. Statement bodies, async/await bodies, null-conditionals, and patterns keep caller text rather than pretending to describe a single called method.

These restrictions are explicit: a source generator can add source files, but cannot rewrite caller-local expressions into new closures. This implementation avoids depending on undocumented compiler closure layouts. See the [Roslyn incremental generator design](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.md).

## Step template diagnostics

The generator validates `[StepText]` attributes at compile time, even when a step has no generated call-site metadata. Diagnostic `BDT002` rejects unknown parameter names, references to hidden `CancellationToken` parameters, positional placeholders and invalid overrides. Templates use `{parameterName}` or `{parameterName:format}`, and overrides use `"parameterName=text"`. Literal JSON and unmatched braces remain text, and `{{` / `}}` escape braces. Override values are never interpreted as templates. See [BDT002](analyzers.md#bdt002) for examples and suppression.

When `BDTest.Analyzers` is installed too, it recognizes the generator's validation marker and does not duplicate these diagnostics. Neither compiler tool executes argument readers, converters or user formatters. Runtime rendering uses the same grammar and records invalid templates as diagnostics without changing the step outcome.

## Validation

`BDTest.SourceGenerator.Tests` runs the generator with Roslyn 4.8, compiles the generated consumer source, loads it, and verifies runtime method identity, attributes, accessors and fallback behavior. The solution and packaging pipeline discover both new projects through their existing conventions. The generator package contains only its analyzer DLL and normal package metadata; consumers retain the core's `netstandard2.0` compatibility.
