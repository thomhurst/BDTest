# BDTest
### A testing framework for .NET Core 2.0+

## About
BDTest is a testing framework. It can be used with other frameworks (such as MSTest, xUnit or NUnit) or standalone.
The examples below will use attributes (such as `[Test]`) from NUnit.

## Installation
Install via Nuget > `Install-Package BDTest`

## Usage
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

### Automatic Context Construction

#### Lambda Syntax
Construction of your TestContext using lambda syntax

```csharp
public class MyTests : BDTestBase
{
    [Test]
    public void Test1() {
        WithContext<TestContext>(context =>
            Given(() => Action1())
            .When(() => Action2())
            .Then(() => Action3())
            .BDTest()
        );
    }
}
```

#### NUnit
Install via Nuget > `Install-Package BDTest.NUnit`

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

 - Your TestContext will be automatically constructed and injected in via the lambda
 - Your TestContext should have a public constructor with 0 parameters

### Thread Safety Parallelization
In order to keep all tests thread safe and have the ability to run all in parallel:
-	Use the `NUnitBDTestBase<>` base class OR `WithContext<TestContext>(...)` syntax as above
-	Do not use static variables/fields/properties in your tests
-	Do not share fields/properties in your test class - All variables should be populated as new for each test - Which the Context construction can take care of

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

## Attributes
### StoryText
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

### Scenario Text
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

### Step Text
Annotate your steps/methods with a `[StepText]` attribute

```csharp
[StepText("I perform my second action")]
public void Action2()
{
    ...
}
```

### Step Text (with parameters)

Use parameter indexes to substitute in your arguments to the steptext

```csharp
[StepText("my name is {0} {1}")]
public void SetName(string firstName, string lastName)
{
    ...
}

public void TestSetName() 
{
    Given(() => SetName("Tom", "Longhurst");) // StepText should equal "Given my name is Tom Longhurst"
    .When(() => SetName("Tom", "Longhurst");) // StepText should equal "When my name is Tom Longhurst"
    .Then(() => SetName("Tom", "Longhurst");) // StepText should equal "Then my name is Tom Longhurst"
    .And(() => SetName("Tom", "Longhurst");) // StepText should equal "And my name is Tom Longhurst"
}
```

# Reports
## Installation
Install via Nuget > `Install-Package BDTest.ReportGenerator`
## Usage
You don't have to do anything. 
Once the package has been installed and your tests have run, these reports should appear in your output directory automatically.

## Persistent Test Data
BDTest allows you to pass it a directory path, to persistently store test data.
Why is this useful?
It allows us to keep a record of all our test runs, and this allows us to compare test runs.

Set the directory by setting `BDTestSettings.PersistentResultsDirectory`

```csharp
[OneTimeSetUp]
public void SetPersistentStorage()
{
    BDTestSettings.PersistentResultsDirectory = "C:\\AcceptanceTests";
}

[Test]
public void Test() 
{
    ...
}
```

This will produce a flakiness report and a test times report.

### Flakiness
In your output directory after your tests have finished:
> BDTest - Report - Flakiness - {timestamp}.html

Override this filename by settings `BDTest.BDTestSettings.FlakinessReportHtmlFilename = "C:\\SomeDirectory\\test-flakiness.html";`

### Test Times
In your output directory after your tests have finished:
> BDTest - Report - Test Times Comparison - {timestamp}.html

Override this filename by settings `BDTest.BDTestSettings.TestTimesReportHtmlFilename = "C:\\SomeDirectory\\test-times.html";`

## Json
In your output directory after your tests have finished:
> BDTest - Test Data - {timestamp}.json

Override this filename by settings `BDTest.BDTestSettings.JsonDataFilename = "C:\\SomeDirectory\\test-json-raw-data.json";`

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


## XML
In your output directory after your tests have finished:
> BDTest - Test Data - {timestamp}.xml

Override this filename by settings `BDTest.BDTestSettings.XmlDataFilename = "C:\\SomeDirectory\\test-xml-raw-data.json";`

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

## HTML
### By Story
In your output directory after your tests have finished:
> BDTest - Report - By Story - {timestamp}.html

Override this filename by settings `BDTest.BDTestSettings.ScenariosByStoryReportHtmlFilename = "C:\\SomeDirectory\\test-stories.html";`

### All Scenarios
In your output directory after your tests have finished:
> BDTest - Report - All Scenarios - {timestamp}.html

Override this filename by settings `BDTest.BDTestSettings.AllScenariosReportHtmlFilename = "C:\\SomeDirectory\\test-all-scenarios.html";`
