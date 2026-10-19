using BDTest;
using BDTest.Settings;
using BDTest.Test;
using BDTest.Tests.Helpers;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[NotInParallel]
[Story(AsA = "BDTest developer",
    IWant = "to make sure that custom step text attributes work properly",
    SoThat = "tests can be translated to plain English")]
public class StepTextTests : RunTestBase
{
    protected override bool BindCurrentTestMetadata => true;

    [After(HookType.Test)]
    public void FinishExecution() => EndTest();

    private InnerStepsClass InnerSteps => new();
        

    [Test]
    public async Task CustomStepTexts_ConstantArgumentValue()
    {
        var scenario = await Given(() => Action1Custom())
            .When(() => StepThatPrintsMyName("Tom", "Longhurst"))
            .Then(() => Action3Custom())
            .BDTestAsync();
            
        await Assert.That(Required(scenario.Steps)[0].StepText).IsEqualTo("Given I have a custom step 1");
        await Assert.That(scenario.Steps[1].StepText).IsEqualTo("When my name is Tom Longhurst");
        await Assert.That(scenario.Steps[2].StepText).IsEqualTo("Then I have a custom step 3");

        await Assert.That(Required(JsonHelper.GetTestDynamicJsonObject(TestRun).SelectToken("$.scenarios[0].steps[0].stepText")).ToString()).IsEqualTo("Given I have a custom step 1");
        await Assert.That(Required(JsonHelper.GetTestDynamicJsonObject(TestRun).SelectToken("$.scenarios[0].steps[1].stepText")).ToString()).IsEqualTo("When my name is Tom Longhurst");
        await Assert.That(Required(JsonHelper.GetTestDynamicJsonObject(TestRun).SelectToken("$.scenarios[0].steps[2].stepText")).ToString()).IsEqualTo("Then I have a custom step 3");
    }

    [Test]
    [Arguments("Tom", "Longhurst")]
    public async Task CustomStepTexts_DynamicArgumentValue(string firstName, string lastName)
    {
        var scenario = await Given(() => Action1Custom())
            .When(() => StepThatPrintsMyName(firstName, lastName)).WithArgs(() => firstName, () => lastName)
            .Then(() => Action3Custom())
            .BDTestAsync();
            
        await Assert.That(Required(scenario.Steps)[0].StepText).IsEqualTo("Given I have a custom step 1");
        await Assert.That(scenario.Steps[1].StepText).IsEqualTo("When my name is Tom Longhurst");
        await Assert.That(scenario.Steps[2].StepText).IsEqualTo("Then I have a custom step 3");

        await Assert.That(Required(JsonHelper.GetTestDynamicJsonObject(TestRun).SelectToken("$.scenarios[0].steps[0].stepText")).ToString()).IsEqualTo("Given I have a custom step 1");
        await Assert.That(Required(JsonHelper.GetTestDynamicJsonObject(TestRun).SelectToken("$.scenarios[0].steps[1].stepText")).ToString()).IsEqualTo("When my name is Tom Longhurst");
        await Assert.That(Required(JsonHelper.GetTestDynamicJsonObject(TestRun).SelectToken("$.scenarios[0].steps[2].stepText")).ToString()).IsEqualTo("Then I have a custom step 3");
    }
        
    [Test]
    public async Task OverriddenStepTexts()
    {
        var text1 = "My first overridden steptext";
        var text2 = "My second overridden steptext";
        var text3 = "My third overridden steptext";
            
        var scenario = await Given(() => Action1Custom()).WithStepText(() => text1)
            .When(() => StepThatPrintsMyName("Tom", "Longhurst")).WithStepText(() => text2)
            .Then(() => Action3Custom()).WithStepText(() => text3)
            .And(() => Action3Custom()).WithStepText(() => text3)
            .BDTestAsync();
            
        await Assert.That(Required(scenario.Steps)[0].StepText).IsEqualTo("Given " + text1);
        await Assert.That(scenario.Steps[1].StepText).IsEqualTo("When " + text2);
        await Assert.That(scenario.Steps[2].StepText).IsEqualTo("Then " + text3);
        await Assert.That(scenario.Steps[3].StepText).IsEqualTo("And " + text3);

        await Assert.That(Required(JsonHelper.GetTestDynamicJsonObject(TestRun).SelectToken("$.scenarios[0].steps[0].stepText")).ToString()).IsEqualTo("Given " + text1);
        await Assert.That(Required(JsonHelper.GetTestDynamicJsonObject(TestRun).SelectToken("$.scenarios[0].steps[1].stepText")).ToString()).IsEqualTo("When " + text2);
        await Assert.That(Required(JsonHelper.GetTestDynamicJsonObject(TestRun).SelectToken("$.scenarios[0].steps[2].stepText")).ToString()).IsEqualTo("Then " + text3);
        await Assert.That(Required(JsonHelper.GetTestDynamicJsonObject(TestRun).SelectToken("$.scenarios[0].steps[3].stepText")).ToString()).IsEqualTo("And " + text3);
    }
        
    [Test]
    public async Task DefaultMethodNameStepTexts()
    {
        var scenario = await Given(() => Step1WithoutAStepTextAttribute())
            .When(() => Step2WithoutAStepTextAttribute())
            .Then(() => Step_3_Without_A_StepTextAttribute_and_Hyphens())
            .BDTestAsync();
            
        await Assert.That(Required(scenario.Steps)[0].StepText).IsEqualTo("Given step 1 without a step text attribute");
        await Assert.That(scenario.Steps[1].StepText).IsEqualTo("When step 2 without a step text attribute");
        await Assert.That(scenario.Steps[2].StepText).IsEqualTo("Then step 3 Without A StepTextAttribute and Hyphens");

        await Assert.That(Required(JsonHelper.GetTestDynamicJsonObject(TestRun).SelectToken("$.scenarios[0].steps[0].stepText")).ToString()).IsEqualTo("Given step 1 without a step text attribute");
        await Assert.That(Required(JsonHelper.GetTestDynamicJsonObject(TestRun).SelectToken("$.scenarios[0].steps[1].stepText")).ToString()).IsEqualTo("When step 2 without a step text attribute");
        await Assert.That(Required(JsonHelper.GetTestDynamicJsonObject(TestRun).SelectToken("$.scenarios[0].steps[2].stepText")).ToString()).IsEqualTo("Then step 3 Without A StepTextAttribute and Hyphens");
    }
        
    [Test]
    public async Task FuncStepTextDoesNotInvokeTheArgument()
    {
        var scenario = await Given(() => Console.WriteLine("I have a func that returns a strring"))
            .When(() => FuncReturningStepText(() => "Blah")).WithArgs(() => (Func<string>)(() => "Blah"))
            .Then(() => Console.WriteLine("the steptext should correctly have the func response"))
            .BDTestAsync();
            
        await Assert.That(Required(scenario.Steps)[1].StepText).IsEqualTo($"When the func returns: {typeof(Func<string>)}");
    }
        
    [Test]
    public async Task WithStepTextList()
    {
        var scenario = await Given(() => Console.WriteLine("Empty Step"))
            .When(() => Console.WriteLine("Empty Step"))
            .Then(() => StepWithListType(new List<string> {"Blah1", "Blah2", "Blah3"})).WithArgs(() => new List<string> {"Blah1", "Blah2", "Blah3"})
            .BDTestAsync();
            
        await Assert.That(Required(scenario.Steps)[2].StepText).IsEqualTo("Then the step text can display the list Blah1, Blah2, Blah3");
    }
        
    [Test]
    public async Task WithCustomStepTextStringConverter()
    {
        Options.RegisterCustomStringConverter(new CustomClassToStepTextStringStringConverter());
            
        var scenario = await Given(() => Console.WriteLine("Empty Step"))
            .When(() => Console.WriteLine("Empty Step"))
            .Then(() => StepWithStrongTypeAndStepText(new CustomClassToStepTextString())).WithArgs(() => new CustomClassToStepTextString())
            .BDTestAsync();
            
        await Assert.That(Required(scenario.Steps)[2].StepText).IsEqualTo("Then the text is 123..................4....................................5");
    }
        
    [Test]
    public async Task NoStepTextIncludesParameterValues()
    {
        var scenario = await When(() => StepWithParametersButWithoutStepText("One", "Two"))
            .Then(() => StepWithParametersButWithoutStepText("One", "Two"))
            .BDTestAsync();
            
        await Assert.That(Required(scenario.Steps)[1].StepText).IsEqualTo("Then step with parameters but without step text One Two");
    }

    [Test]
    public async Task StepTextForStrongTypeWithoutConvertor()
    {
                        
        var scenario = await Given(() => Console.WriteLine("Empty Step"))
            .When(() => Console.WriteLine("Empty Step"))
            .Then(() => StepWithStrongTypeAndStepText(new CustomClassToStepTextString
            {
                One = "1", Two = "2", Three = "3", Four = "4", Five = "5"
            })).WithArgs(() => new CustomClassToStepTextString
            {
                One = "1", Two = "2", Three = "3", Four = "4", Five = "5"
            })
            .BDTestAsync();

        var thenStep = Required(scenario.Steps).Last();
            
        await Assert.That(thenStep.StepText).IsEqualTo("Then the text is Out of the box ToString() called");
    }
        
    [Test]
    public async Task StepTextForStrongTypeInInnerStepsClassWithoutConvertor()
    {
                        
        var scenario = await Given(() => Console.WriteLine("Empty Step"))
            .When(() => Console.WriteLine("Empty Step"))
            .Then(() => InnerSteps.StepWithStrongTypeAndStepText(new CustomClassToStepTextString
            {
                One = "1", Two = "2", Three = "3", Four = "4", Five = "5"
            })).WithArgs(() => new CustomClassToStepTextString
            {
                One = "1", Two = "2", Three = "3", Four = "4", Five = "5"
            })
            .BDTestAsync();

        var thenStep = Required(scenario.Steps).Last();
            
        await Assert.That(thenStep.StepText).IsEqualTo("Then the text is Out of the box ToString() called");
    }
        
    [Test]
    public async Task StepTextForStrongTypeInInnerStepsWithoutStepTextAttribute()
    {
                        
        var scenario = await Given(() => Console.WriteLine("Empty Step"))
            .When(() => Console.WriteLine("Empty Step"))
            .Then(() => InnerSteps.StepWithStrongTypeAndNoStepText(new CustomClassToStepTextString
            {
                One = "1", Two = "2", Three = "3", Four = "4", Five = "5"
            })).WithArgs(() => new CustomClassToStepTextString
            {
                One = "1", Two = "2", Three = "3", Four = "4", Five = "5"
            })
            .BDTestAsync();

        var thenStep = Required(scenario.Steps).Last();
            
        await Assert.That(thenStep.StepText).IsEqualTo("Then step with strong type and no step text Out of the box ToString() called");
    }

    private int _retryCount;
    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    [BDTestRetry(3)]
    public async Task RetrySuccessfully(int throwIfRetryLessThan)
    {
        var scenario = await Given(() => InnerSteps.StepWithStrongTypeAndNoStepText(new CustomClassToStepTextString
            {
                One = "1", Two = "2", Three = "3", Four = "4", Five = "5"
            })).WithArgs(() => new CustomClassToStepTextString
            {
                One = "1", Two = "2", Three = "3", Four = "4", Five = "5"
            })
            .When(() => ThrowException(throwIfRetryLessThan))
            .Then(() => Console.WriteLine("the test passed"))
            .BDTestAsync();
            
        var thenStep = Required(scenario.Steps).First();
            
        await Assert.That(thenStep.StepText).IsEqualTo("Given step with strong type and no step text Out of the box ToString() called");
    }
        
    private void ThrowException(int throwIfRetryLessThan)
    {
        if (_retryCount++ < throwIfRetryLessThan)
        {
            throw new MyCustomRetryException("Blah");
        }
    }
        
    [StepText("the text is {0}")]
    private void StepWithStrongTypeAndStepText(CustomClassToStepTextString obj)
    {
    }

    [StepText("the func returns: {0}")]
    private void FuncReturningStepText(Func<string> func)
    {
    }
        
    [StepText("the step text can display the list {0}")]
    private void StepWithListType(IList<string> list)
    {
    }

    [StepText("I have a custom step 1")]
    private void Action1Custom()
    {
    }
        
    [StepText("my name is {0} {1}")]
    private void StepThatPrintsMyName(string firstName, string lastName)
    {
    }
        
    [StepText("I have a custom step 3")]
    private void Action3Custom()
    {
    }
        
    private void Step1WithoutAStepTextAttribute()
    {
    }
        
    private void Step2WithoutAStepTextAttribute()
    {
    }
        
    private void Step_3_Without_A_StepTextAttribute_and_Hyphens()
    {
    }

    private void StepWithParametersButWithoutStepText(string valueOne, string valueTwo)
    {
    }

    private class InnerStepsClass
    {
        [StepText("the text is {0}")]
        public void StepWithStrongTypeAndStepText(CustomClassToStepTextString obj)
        {
        }
            
        public void StepWithStrongTypeAndNoStepText(CustomClassToStepTextString obj)
        {
        }
    }
}
