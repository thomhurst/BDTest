---
title: Stories and scenarios
description: Describe why a group of tests exists with [Story] and what each test does with [ScenarioText].
---

# Stories and scenarios

Reports group scenarios by **story**. A story explains who benefits from a feature and why. A scenario describes one example of that feature.

## `[Story]`

Put `[Story]` on a test class. Set `AsA` (or `AsAn`), `IWant` and `SoThat`:

```csharp title="Illustrative fragment"
[Story(AsA = "registered customer",
    IWant = "to reset my password",
    SoThat = "I can sign in again if I forget it")]
public class PasswordResetTests : TUnitBDTestBase<MyTestContext>
{
    ...
}
```

The report shows:

```text
As a registered customer
I want to reset my password
So that I can sign in again if I forget it
```

BDTest adds the "As a", "I want" and "So that" prefixes unless your text already starts with them.

- Use `AsAn` when "As an" reads better: `AsAn = "authenticated customer"`.
- For roles without an explicit article, `AsA` uses "an" before a vowel letter and "a" otherwise. Roles starting with "user" use "a". Explicit articles and complete prefixes are preserved.
- `AsA`/`AsAn`, `IWant` and `SoThat` are required. A missing value throws `MissingStoryTextArgumentException`. Setting both `AsA` and `AsAn` throws `DuplicateStoryTextArgumentException`.

Formatting does not change the attribute values. Prefix checks match whole words, ignoring case. See [story text formatting](../attributes/story-text.md) for the complete article and prefix rules.

Classes without `[Story]` are grouped without story text.

## `[ScenarioText]`

Put `[ScenarioText]` on a test method to name the scenario:

```csharp title="Illustrative fragment"
[Test]
[ScenarioText("A reset link is emailed to a registered address")]
public async Task ResetLinkIsEmailed()
{
    await Given(() => Customers.ARegisteredCustomer())
        .When(() => Customers.RequestAPasswordReset())
        .Then(() => Emails.AResetLinkWasSent());
}
```

With a framework adapter, the runner supplies the exact test method even after async suspension or when construction happens in a helper. Attributes on helper methods are ignored. Metadata is cached per method and concrete fixture type. A null attribute text falls back to the humanized method name; an empty string remains empty.

Without the attribute, BDTest humanises the test method name: `ResetLinkIsEmailed` becomes "Reset link is emailed".

Scenario text uses the same [name splitting rules as step text](step-text.md#default-text-from-method-names), with the first letter capitalized. For example, `user_logs_in` becomes "User logs in", `MFAToken` becomes "MFA token", and `Order2IsShipped` becomes "Order 2 is shipped". Underscore-separated parts retain their casing; PascalCase and camelCase words are lowercased except acronyms. See the [migration notes](../migration/generated-name-text.md) for differences from earlier versions.

You can also set the text on the chain with `.WithScenarioText("...")` after the `Then` steps. For scenario outlines, the scenario text is the first argument of `ScenarioOutline(...)`.

## Reading story and scenario text

`BDTestBase.GetStoryText()` and `GetScenarioText()` return the resolved text for the current test. `BDTestContext` exposes the same methods, so a [`ConfigureContext`](test-context.md#configuring-a-new-context) or step hook can read them.

Without an adapter or explicit `BeginTest(id, methodInfo)`, BDTest uses the compiler-supplied caller name and file, without discovering method attributes. See [custom adapters](../frameworks/custom-adapters.md#custom-harnesses-without-a-runner).
