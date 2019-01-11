# BDTest
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

 - Your TestContext will be automatically constructed and injected in via the lambda
 - Your TestContext should have a public constructor with 0 parameters

### Thread Safety Parallelization
In order to keep all tests thread safe and have the ability to run all in parallel:
-	Use the `WithContext<TestContext>(...)` syntax as above
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

# Reports
## Installation
Install via Nuget > `Install-Package BDTest.ReportGenerator`
## Usage
You don't have to do anything. 

Once the package has been installed and your tests have run, these reports should appear in your output directory automatically.
## Json
In your output directory after your tests have finished:
> test_data - {timestamp}.json

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
> test_data - {timestamp}.xml


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
> Report - By Story - {timestamp}.html

### All Scenarios
In your output directory after your tests have finished:
> Report - All Scenarios - {timestamp}.html
