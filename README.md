# BDTest
## A testing framework for .NET

[![nuget](https://img.shields.io/nuget/v/BDTest.svg)](https://www.nuget.org/packages/BDTest/)
[![Codacy Badge](https://api.codacy.com/project/badge/Grade/976b0c6b323b43ef94334f503af9b737)](https://www.codacy.com/app/thomhurst/BDTest?utm_source=github.com&amp;utm_medium=referral&amp;utm_content=thomhurst/BDTest&amp;utm_campaign=Badge_Grade)

## About

BDTest is a testing framework. It can be used with other frameworks (such as MSTest, xUnit or NUnit) or standalone.
The examples below will use attributes (such as `[Test]`) from NUnit.

BDTest is written in .NET Standard - So should work for .NET Framework and .NET Core

### Installation

Install via Nuget > `Install-Package BDTest`

### Usage

Tests can be constructed either by extending from a base class, or by using the a test builder object.

Extending from `BDTestBase`

```csharp
public class MyTests : BDTestBase
{
    [Test]
    public void Test1() {
        Given(() => Action1())
        .When(() => Action2())
        .Then(() => Action3())
        .BDTest();
    }
}
```

Or by using the `BDTestBuilder` object

```csharp
public class MyTests 
{
    [Test]
    public void Test1() {
        new BDTestBuilder().Given(() => Action1())
                            .When(() => Action2())
                            .Then(() => Action3())
                            .BDTest();
    }
}
```

### Async

BDTest supports asynchronous operations. In any Given, When, Then, you can pass in an asynchronous, or a non-asynchronous call.
If you call `.BDTest()` at the end, any asynchronous tasks will be run in a blocking mode.
If you want to utilise full asynchronous calls, use `.BDTestAsync()`
To use this to the full extent, try to use asynchronous methods absolutely everywhere that they could be used.

Make sure if calling a test using `.BDTestAsync()` to await the test, and make the test `async` and return a `Task` instead of void.

```csharp
public class MyTests : BDTestBase
{
    [Test]
    public async Task Test1() {
       await Given(() => Action1Async())
             .When(() => Action2Async())
             .Then(() => Action3Async())
             .BDTestAsync();
    }
}
```

### Automatic Context Construction

#### Lambda Syntax

Construction of your TestContext using lambda syntax

```csharp
public class MyTests : BDTestBase
{
    [Test]
    public void Test1() {
        WithContext<TestContext>(context =>
            Given(() => Action1(context))
            .When(() => Action2(context))
            .Then(() => Action3(context))
            .BDTest()
        );
    }
}
```

- Your TestContext will be automatically constructed and injected in via the lambda

#### NUnit

Instead of extending from `BDTestBase` extend from `NUnitBDTestBase` and pass the type of your Context.
Your context will be constructed for each test independently.

Access this using the `Context` property. See below for example.

```csharp
    public class TestsUsingNUnitBaseWithContext : NUnitBDTestBase<TestContext>
    {
        [Test]
        public void Test1()
        {
            Given(() => Action1(Context))
                .When(() => Action2(Context))
                .Then(() => Action3(Context))
                .BDTest();
        }
    }
```

For either of these methods, your TestContext should have a public constructor with 0 parameters. Otherwise, check out the source code for NUnitBDTestBase to set up your own version with extra construction logic.

### Thread Safety Parallelization

In order to keep all tests thread safe and have the ability to run all in parallel:
- Use the `NUnitBDTestBase<>` base class OR `WithContext<TestContext>(...)` syntax as above
- Do not use static variables/fields/properties in your tests
- Do not share fields/properties in your test class - All variables should be populated as new for each test - Which the Context construction can take care of

## Best Practice

BDTest enforces best practice: 
- Tests MUST contain a `Given` + `When` + `Then` and executed with a `BDTest`
- Tests MUST start with a `Given`
	- Off of a `Given` you can have `And` or `When`
	  - Off of an `And` you can  have `And` or `When`
	- Off of a `When` you can have a `Then` 
	  - (No `And` - We should be testing one action!)
	- Off of a `Then` you can have `And` or `BDTest`
	  - Off of an `And` you can  have `And` or `BDTest`

### Attributes

#### StoryText

Annotate your test classes with a `[StoryText]` attribute

```csharp
[Story(AsA = "Test User",
    IWant = "To Test",
    SoThat = "Things Work")]
public class MyTests : BDTestBase
{
    ...
}
```

#### Scenario Text

Annotate your tests with a `[ScenarioText]` attribute

```csharp
[Test]
[ScenarioText("Custom Scenario")]
public void Test1()
{
    Given(() => Action1())
        .When(() => Action2())
        .Then(() => Action3())
        .And(() => Action4())
        .BDTest();
}
```

#### Step Text

Annotate your steps/methods with a `[StepText]` attribute

```csharp
[StepText("I perform my second action")]
public void Action2()
{
    ...
}
```

#### Step Text (with parameters)

Use parameter indexes to substitute in your arguments to the steptext

```csharp
[StepText("my name is {0} {1}")]
public void SetName(string firstName, string lastName)
{
    ...
}

public void TestSetName() 
{
    Given(() => SetName("Tom", "Longhurst")) // StepText should equal "Given my name is Tom Longhurst"
    .When(() => SetName("Tom", "Longhurst")) // StepText should equal "When my name is Tom Longhurst"
    .Then(() => SetName("Tom", "Longhurst")) // StepText should equal "Then my name is Tom Longhurst"
    .And(() => SetName("Tom", "Longhurst")) // StepText should equal "And my name is Tom Longhurst"
}
```

#### Overriding Step Text

Sometimes, you can't generate a nice step text from attributes due to compile-time constant restraints,
or you might have a method that takes a Func<> and won't implicitly convert to a nice readable string.

The way around this would be to add a `.WithStepText(() => "text")` call to your step.

For instance, I have a method which takes a Func, and it's used to update fields on an API Request Model. I did this so that I don't have to create lots of different methods doing a similar thing. The trade off here was that I can't have a StepText that specifically outlined what I was doing for each test.

So I can override this to a different value for each test, and provide a better context to what action I am performing.
This looks like:
```
.When(() => MyUpdateSteps.UpdateTheField(request => nameof(request.EmailAddress), newEmailAddress)).WithStepText(() => $"I call update customer with a new email address of '{newEmailAddress}'")
```

## Reports

### Report Installation

Install via Nuget > `Install-Package BDTest.ReportGenerator`

### Report Usage

You will need to create a global tear down method that runs after all of your tests, and in that you need to call
`BDTestReportGenerator.Generate();`

Example in NUnit would be a class like this:

```csharp
    [SetUpFixture]
    public class TestSetup
    {
        [OneTimeTearDown]
        public void GlobalTeardown()
        {
            BDTestReportGenerator.Generate();
        }
    }
```

See below for extra options.

### Persistant Test Data

BDTest allows you to pass it a directory path, to persistantly store test data.
Why is this useful?
It allows us to keep a record of all our test runs, and this allows us to compare test runs.

Set the directory by setting `BDTestSettings.PersistantResultsDirectory`

```csharp
[OneTimeSetUp]
public void SetPersistantStorage()
{
    BDTestSettings.PersistantResultsDirectory = "C:\\AcceptanceTests";
}

[Test]
public void Test() 
{
    ...
}
```

If you set these, you will be produced a Test Time Comparison Report and a Test Flakiness Report.

#### Test Flakiness

In your output directory after your tests have finished:
> BDTest - Report - Test Times Comparison - {timestamp}.html

#### Test Times Comparison

In your output directory after your tests have finished:
> BDTest - Report - Test Times Comparison - {timestamp}.html

### Json

In your output directory after your tests have finished:

> BDTest - Test Data - {timestamp}.json

<details>
<summary>Example:</summary>
<p>

```json
    {
    "TestTimer": {
    
    "TestsStartedAt": "2019-01-11T10:23:50.0230533+00:00",
    
    "TestsFinishedAt": "2019-01-11T10:23:50.6290701+00:00",
    
    "ElapsedTime": "00:00:00.6060168"
    
    },
    
    "Scenarios": [
    
    {
    
    "StartTime": "2019-01-11T10:23:50.551935+00:00",
    
    "EndTime": "2019-01-11T10:23:50.553344+00:00",
    
    "TimeTaken": "0.00:00:00:0014090",
    
    "StoryText": {
    
    "Story": "As a Test User\r\nI want To Test\r\nSo that Things Work\r\n"
    
    },
    
    "ScenarioText": {
    
    "Scenario": "Custom Scenario"
    
    },
    
    "Steps": [
    
    {
    
    "StartTime": "2019-01-11T10:23:50.5519411+00:00",
    
    "EndTime": "2019-01-11T10:23:50.5522994+00:00",
    
    "_stepType": "Given",
    
    "TimeTaken": "0.00:00:00:0003583",
    
    "Exception": null,
    
    "Status": "Passed",
    
    "StepText": "Given Action 1"
    
    },
    
    {
    
    "StartTime": "2019-01-11T10:23:50.5523005+00:00",
    
    "EndTime": "2019-01-11T10:23:50.5528788+00:00",
    
    "_stepType": "When",
    
    "TimeTaken": "0.00:00:00:0005783",
    
    "Exception": null,
    
    "Status": "Passed",
    
    "StepText": "When Action 2 is custom text"
    
    },
    
    {
    
    "StartTime": "2019-01-11T10:23:50.5528798+00:00",
    
    "EndTime": "2019-01-11T10:23:50.5531625+00:00",
    
    "_stepType": "Then",
    
    "TimeTaken": "0.00:00:00:0002827",
    
    "Exception": null,
    
    "Status": "Passed",
    
    "StepText": "Then Action 3"
    
    },
    
    {
    
    "StartTime": "2019-01-11T10:23:50.5531635+00:00",
    
    "EndTime": "2019-01-11T10:23:50.5533388+00:00",
    
    "_stepType": "AndThen",
    
    "TimeTaken": "0.00:00:00:0001753",
    
    "Exception": null,
    
    "Status": "Passed",
    
    "StepText": "And Action 3"
    
    }
    
    ],
    
    "Status": "Passed"
    
    }
    
    ]
    
    }
```
</p>
</details>

### XML

In your output directory after your tests have finished:

> BDTest - Test Data - {timestamp}.xml

<details>
<summary>Example:</summary>
<p>

```xml
    <?xml version="1.0" encoding="utf-16"?>
    
    <TestData>
    
    <TestTimer>
    
    <TestsStartedAt>2019-01-11T10:23:50.0230533+00:00</TestsStartedAt>
    
    <TestsFinishedAt>2019-01-11T10:23:50.6290701+00:00</TestsFinishedAt>
    
    <ElapsedTime>00:00:00.6060168</ElapsedTime>
    
    </TestTimer>
    
    <Scenarios>
    
    <StartTime>2019-01-11T10:23:50.551935+00:00</StartTime>
    
    <EndTime>2019-01-11T10:23:50.553344+00:00</EndTime>
    
    <TimeTaken>0.00:00:00:0014090</TimeTaken>
    
    <StoryText>
    
    <Story>As a Test User
    
    I want To Test
    
    So that Things Work
    
    </Story>
    
    </StoryText>
    
    <ScenarioText>
    
    <Scenario>Custom Scenario</Scenario>
    
    </ScenarioText>
    
    <Steps>
    
    <StartTime>2019-01-11T10:23:50.5519411+00:00</StartTime>
    
    <EndTime>2019-01-11T10:23:50.5522994+00:00</EndTime>
    
    <_stepType>Given</_stepType>
    
    <TimeTaken>0.00:00:00:0003583</TimeTaken>
    
    <Exception  />
    
    <Status>Passed</Status>
    
    <StepText>Given Action 1</StepText>
    
    </Steps>
    
    <Steps>
    
    <StartTime>2019-01-11T10:23:50.5523005+00:00</StartTime>
    
    <EndTime>2019-01-11T10:23:50.5528788+00:00</EndTime>
    
    <_stepType>When</_stepType>
    
    <TimeTaken>0.00:00:00:0005783</TimeTaken>
    
    <Exception  />
    
    <Status>Passed</Status>
    
    <StepText>When Action 2</StepText>
    
    </Steps>
    
    <Steps>
    
    <StartTime>2019-01-11T10:23:50.5528798+00:00</StartTime>
    
    <EndTime>2019-01-11T10:23:50.5531625+00:00</EndTime>
    
    <_stepType>Then</_stepType>
    
    <TimeTaken>0.00:00:00:0002827</TimeTaken>
    
    <Exception  />
    
    <Status>Passed</Status>
    
    <StepText>Then Action 3</StepText>
    
    </Steps>
    
    <Steps>
    
    <StartTime>2019-01-11T10:23:50.5531635+00:00</StartTime>
    
    <EndTime>2019-01-11T10:23:50.5533388+00:00</EndTime>
    
    <_stepType>AndThen</_stepType>
    
    <TimeTaken>0.00:00:00:0001753</TimeTaken>
    
    <Exception  />
    
    <Status>Passed</Status>
    
    <StepText>And Action 4</StepText>
    
    </Steps>
    
    <Status>Passed</Status>
    
    </Scenarios>
    
    </TestData>
```
</p>
</details>

### HTML

#### By Story

In your output directory after your tests have finished:
> BDTest - Report - By Story - {timestamp}.html

#### All Scenarios

In your output directory after your tests have finished:
> BDTest - Report - All Scenarios - {timestamp}.html
