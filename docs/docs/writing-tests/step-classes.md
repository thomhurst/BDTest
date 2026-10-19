---
title: Step classes
description: Organise steps into reusable classes that operate on the test context.
---

# Step classes

Put steps into classes grouped by area, for example `AccountSteps`, `PaymentSteps` and `HttpAssertions`. Each class takes the [test context](test-context.md) in its constructor so its steps can read and change the state of the current test.

```csharp title="Illustrative fragment"
public class AccountSteps
{
    private readonly MyTestContext _context;

    public AccountSteps(MyTestContext context)
    {
        _context = context;
    }

    [StepText("I create a new account")]
    public async Task CreateAnAccount()
    {
        _context.Response = await _context.Client.PostAsJsonAsync("/accounts", new
        {
            FirstName = "Tom",
            LastName = "Longhurst",
        });
    }
}
```

## Exposing steps to tests

Define step classes as **properties** that create a new instance in the getter. Every access then binds the steps to the context of the test that is running.

```csharp title="Illustrative fragment"
public abstract class MyTestBase : TUnitBDTestBase<MyTestContext>
{
    // Properties, not fields: each access uses the current test's Context.
    protected AccountSteps AccountSteps => new(Context);
    protected AccountAssertions AccountAssertions => new(Context);
    protected HttpAssertions HttpAssertions => new(Context);
}
```

:::warning Do not use fields
A field such as `private readonly AccountSteps _steps = new(Context);` is created once per test class instance, before the test's context exists. Depending on the framework's instance lifetime, tests could then share state. Always use a getter property.
:::

Construct helpers explicitly as above, or resolve them through your service provider. `Inject<T>()` has been removed.

## Using steps in tests

```csharp title="Illustrative fragment"
public class AccountTests : MyTestBase
{
    [Test]
    [ScenarioText("Creating an account returns OK")]
    public async Task CreateAccountSuccessfully()
    {
        await When(() => AccountSteps.CreateAnAccount())
            .Then(() => HttpAssertions.TheHttpStatusCodeIs(HttpStatusCode.OK))
            .BDTestAsync();
    }
}
```

## Tips

- Separate **actions** from **assertions**. Assertion classes read naturally after `Then`.
- Any method can be used in any position. You do not mark a method as a `Given` or `When` step.
- Steps can be synchronous or return `Task`. See [Asynchronous steps](async-steps.md).
- Steps can be private methods on the test class. `[StepText]` still applies.
