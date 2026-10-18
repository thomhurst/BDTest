---
title: Test context
description: Give every test its own state object so scenarios run in parallel without leaking state.
---

# Test context

A context is an object that holds the state of one test: request models, HTTP responses, created IDs and so on. BDTest creates a separate context for each test and never shares it with other tests. That keeps tests independent and lets them run in parallel.

Your context class needs a public parameterless constructor:

```csharp
public class MyTestContext
{
    public HttpRequestMessage Request { get; set; }
    public HttpResponseMessage Response { get; set; }
    public string CustomerId { get; set; }
}
```

## Using a base class

Every [framework adapter](../frameworks/overview.md) takes the context type as a generic argument. Read it through the `Context` property:

```csharp
public class AccountTests : TUnitBDTestBase<MyTestContext>
{
    [Test]
    public async Task CreateAccount()
    {
        await When(() => CreateAccount(Context))
            .Then(() => TheResponseIsOk(Context))
            .BDTestAsync();
    }
}
```

The adapter keys each context by the framework's test ID, creates it on first access and removes it after the test. Adapters derive from `AbstractContextBDTestBase<TContext>` in the core package, which you can also use for [custom adapters](../frameworks/custom-adapters.md).

## Using `WithContext`

Without a context base class, ask for a context inline:

```csharp
public class MyTests : BDTestBase
{
    [Test]
    public async Task Test1()
    {
        await WithContext<MyTestContext>(context =>
            Given(() => Action1(context))
                .When(() => Action2(context))
                .Then(() => Action3(context))
                .BDTestAsync());
    }
}
```

BDTest constructs the context and passes it to the lambda.

## Amending a new context

Set `ContextAmendment` to run code each time BDTest creates a context. Use it for defaults that need more than a parameterless constructor:

```csharp
public abstract class MyTestBase : TUnitBDTestBase<MyTestContext>
{
    protected MyTestBase()
    {
        ContextAmendment = bdTestContext =>
        {
            bdTestContext.TestContext.Request = new HttpRequestMessage();
        };
    }
}
```

The delegate receives a `BDTestContext<TContext>`. Its `TestContext` property is your context object; it also exposes the story text, scenario text and current scenario.

`ContextAmendment` runs for:

- the first context of each test,
- the new context created before each [retry](retries.md),
- each row of a [scenario outline](scenario-outlines.md).

If the amendment throws, BDTest discards that context. During amendment for an outline row, `BDTestContext.CurrentScenario` is `null` because the row's scenario does not exist yet.

## Other context members

| Member | Purpose |
| --- | --- |
| `Context` | The current test's context. |
| `BDTestContext` | The wrapper holding `TestContext` plus story and scenario information. |
| `OverwriteContext(TContext)` | Replace the current test's context with your own instance. |
| `Inject<T>()` | Create a `T`, passing `Context` to its constructor when it has a `TContext` parameter, otherwise using its parameterless constructor. |

## Rules for parallel tests

To keep tests thread-safe and parallelisable:

- Use a context base class or `WithContext<T>(...)`.
- Do not use static fields or properties for test state. Store it in the context.
- Do not store test state in fields of the test class. Store it in the context.
- Expose [step classes](step-classes.md) as properties that create a new instance on each access.
