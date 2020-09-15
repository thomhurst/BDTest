# BDTest
## A testing and reporting framework for .NET

[![nuget](https://img.shields.io/nuget/v/BDTest.svg)](https://www.nuget.org/packages/BDTest/)
[![Codacy Badge](https://api.codacy.com/project/badge/Grade/976b0c6b323b43ef94334f503af9b737)](https://www.codacy.com/app/thomhurst/BDTest?utm_source=github.com&amp;utm_medium=referral&amp;utm_content=thomhurst/BDTest&amp;utm_campaign=Badge_Grade)
![Nuget](https://img.shields.io/nuget/dt/BDTest)

### Support

If you like using BDTest, consider buying me a coffee :)

<a href="https://www.buymeacoffee.com/tomhurst" target="_blank"><img src="https://www.buymeacoffee.com/assets/img/custom_images/orange_img.png" alt="Buy Me A Coffee" style="height: auto !important;width: auto !important;" ></a>

### Wiki

Please view the [wiki](https://github.com/thomhurst/BDTest/wiki) to see how to use BDTest and how to get setup: https://github.com/thomhurst/BDTest/wiki

### What is it?
BDTest is a testing and reporting framework focusing on 'Behaviour Driven Testing' ideologies. 
It's a way to structure and run your tests so that:
- They are clear, concise and easy to understand
- They are easy to run and debug
- They don't share state. They are independent, side-effect free and able to run in parallel, meaning faster and more stable tests!
- They translate into business criteria and focus on how the system under test would be used by an end user and their behaviours
- You have an easy way to see test results and share them with your team or business

BDTest can be used standalone, or alongside an existing test running framework. For best results, I recommend NUnit, and installing the BDTest.NUnit package alongside the core BDTest package.

#### Why?
I was originally inspired to build BDTest due to SpecFlow.
My team at work used SpecFlow (as does much of the .NET based industry) for their tests when I joined, and upon using it, realised I really didn't enjoy using it and found it to be difficult to work with, finnicky and long-winded.
It turned out, my team also really disliked it, so I thought I'd change things.

Think of BDTest as a pure code-based alternative to SpecFlow.

### What's the benefit over SpecFlow?
- Passing in more complicated arguments to tests. Enums, models, etc.
- You get the benefit of separating steps into Given, When, Then. However you can use and re-use existing steps without redefining or attributing them as Given step, or a When step, etc.
- No auto-generated code. All code is your code! So you shouldn't see any weirdness. This also means no extra weird compilation steps and bringing in specific test compilers/runners.
- Easier to debug. Test has failed and thrown an exception? The stacktrace should take you straight to where it happened. No being taken to strange auto-generated classes that you don't care about!
- Step into all your code. I used Rider as my IDE, which didn't support SpecFlow, so I couldn't step into my test methods. I had to do a painful Find All! Now as it's all code, I can step straight into my test methods.
- No learning and maintaining separate Gherkin files!

### But non-technical can write tests using SpecFlow
- Does that EVER actually happen though?
- They can only use the high level methods. Any new methods, a developer will still need to code the steps for them. If they can only use the high level methods, make the method names human readable, and they can still use them!

### I need reports that translates to business acceptance criteria
- BDTest does that!
- BDTest produces you a clear, easy to understand report, showing what failed and what passed, with output and exceptions from tests automatically captured
- Mark your classes and methods with [StoryText], [ScenarioText], and [StepText] attributes, providing the output you want, and your reports will produce clean text output
- If you don't mark your methods with attributes, BDTest will automatically convert the method name to text (separating words by capitalisation or underscores)
- If you use the report server, you can just send the relevant party a URL directly to your test report!

## Where can I use it?
BDTest is written in .NET Standard, so you can use it in either .NET Framework or .NET Core

## How do I get started?

View the [wiki](https://github.com/thomhurst/BDTest/wiki) to see how to use BDTest and how to get setup: https://github.com/thomhurst/BDTest/wiki
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
            await When(() => AccountSteps.CreateAnAccount())
                .Then(() => HttpAssertions.TheHttpStatusCodeIs(HttpStatusCode.NotFound))
                .BDTestAsync();
        }
        
        [Test]
        [BugInformation("123456")]
        [ScenarioText("A failing test using steps defined in a BDTestBase, with StoryText, ScenarioText and StepTexts")]
        public async Task TestFail()
        {
            await When(() => AccountSteps.CreateAnAccount())
                .Then(() => HttpAssertions.TheHttpStatusCodeIs(HttpStatusCode.OK))
                .BDTestAsync();
        }
    }
}
```

### Report Server Example Image
![Report Server Example Image](https://github.com/thomhurst/BDTest/blob/master/Images/Test%20Luns%20List.png?raw=true)
