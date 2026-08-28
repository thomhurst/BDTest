# Step Chaining Feature

BDTest now supports simple step chaining as an alternative to the traditional Given/When/Then pattern. This allows you to create tests using a more flexible step-based approach.

## Usage

### Traditional BDD Pattern
```csharp
[Test]
public async Task TraditionalBDDTest()
{
    await Given(() => Steps.CreateAnAccount())
        .When(() => Steps.NavigateToHomePage())
        .Then(() => Assertions.TheHttpStatusCodeIs(HttpStatusCode.OK))
        .BDTestAsync();
}
```

### New Step Chaining Pattern
```csharp
[Test]
public async Task StepChainingTest()
{
    await Step(() => Steps.CreateAnAccount())
        .Step(() => Steps.NavigateToHomePage())
        .Step(() => Assertions.TheHttpStatusCodeIs(HttpStatusCode.OK))
        .BDTestAsync();
}
```

## Features

- **Flexible Structure**: Create tests without being constrained by Given/When/Then semantics
- **Unlimited Chaining**: Chain as many steps as needed
- **Full Compatibility**: All existing BDTest features work with step chaining
- **Reporting**: Steps appear in reports with "Step" prefix instead of "Given"/"When"/"Then"
- **Custom Step Text**: Use `WithStepText()` for custom step descriptions

## Examples

### Simple Step Chain
```csharp
await Step(() => CreateUser())
    .Step(() => LoginUser())
    .Step(() => VerifyDashboard())
    .BDTestAsync();
```

### Step Chain with Custom Text
```csharp
await Step(() => CreateUser())
    .WithStepText(() => "Create a new user account")
    .Step(() => LoginUser())
    .WithStepText(() => "Login with the new account")
    .BDTestAsync();
```

### Single Step Test
```csharp
await Step(() => PerformQuickCheck())
    .BDTestAsync();
```

## Backward Compatibility

The step chaining feature is fully backward compatible. All existing Given/When/Then tests continue to work exactly as before. You can even mix both patterns in the same test suite.