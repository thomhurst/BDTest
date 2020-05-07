# BDTest
## A testing and reporting framework for .NET

[![nuget](https://img.shields.io/nuget/v/BDTest.svg)](https://www.nuget.org/packages/BDTest/)
[![Codacy Badge](https://api.codacy.com/project/badge/Grade/976b0c6b323b43ef94334f503af9b737)](https://www.codacy.com/app/thomhurst/BDTest?utm_source=github.com&amp;utm_medium=referral&amp;utm_content=thomhurst/BDTest&amp;utm_campaign=Badge_Grade)

### Wiki

Please view the [wiki](https://github.com/thomhurst/BDTest/wiki) to see how to use BDTest and how to get setup: https://github.com/thomhurst/BDTest/wiki

### What is it?

BDTest is a testing and reporting framework for .NET

#### Purpose
- Fast Test Execution
- Clean and Organised Tests
- Tests That Are Clear, Concise and Make Sense
- Tests That Follow Business Acceptance Criteria
- Plain Human Readable Test Reports

#### Why?
I worked on some projects that had SpecFlow tests, and I had a few problems with it:
- Hard to debug - Exception stacktraces took you to horrible dynamically generated classes
- IDE Support / Plugins - I didn't use Visual Studio, and so I didn't have proper support for specflow  
- Just generally hard to navigate - Especially with no IDE support, so I couldn't go to step implementations automatically!
- Felt slow and bloated

#### What next?

View the [wiki!](https://github.com/thomhurst/BDTest/wiki)

#### Example Test

```csharp
namespace BDTest.Example
{
    [Story(AsA = "BDTest author",
        IWant = "to show an example of how to use the framework",
        SoThat = "people can get set up easier")]
    public class ExampleTest : MyTestBase
    {
        [Test]
        [ScenarioText("A passing test using steps defined in a BDTestBase, with StoryText, ScenarioText and StepTexts")]
        public async Task TestPass()
        {
            await When(() => Steps.CreateAnAccount())
                .Then(() => Assertions.TheHttpStatusCodeIs(HttpStatusCode.NotFound))
                .BDTestAsync();
        }
        
        [Test]
        [BugInformation("123456")]
        [ScenarioText("A failing test using steps defined in a BDTestBase, with StoryText, ScenarioText and StepTexts")]
        public async Task TestFail()
        {
            await When(() => Steps.CreateAnAccount())
                .Then(() => Assertions.TheHttpStatusCodeIs(HttpStatusCode.OK))
                .BDTestAsync();
        }
    }
}
```
