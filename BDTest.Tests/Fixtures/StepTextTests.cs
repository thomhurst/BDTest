using System;
using System.Collections.Generic;
using System.Linq;
using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Settings;
using BDTest.Test;
using BDTest.Tests.Helpers;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures
{
    [Parallelizable(ParallelScope.None)]
    [Story(AsA = "BDTest developer",
        IWant = "to make sure that custom step text attributes work properly",
        SoThat = "tests can be translated to plain English")]
    public class StepTextTests : BDTestBase
    {
        private InnerStepsClass InnerSteps => new InnerStepsClass();
        
        [SetUp]
        public void Setup()
        {
            TestResetHelper.ResetData();
        }

        [Test]
        public void CustomStepTexts_ConstantArgumentValue()
        {
            var scenario = Given(() => Action1Custom())
                .When(() => StepThatPrintsMyName("Tom", "Longhurst"))
                .Then(() => Action3Custom())
                .BDTest();
            
            Assert.That(scenario.Steps[0].StepText, Is.EqualTo("Given I have a custom step 1"));
            Assert.That(scenario.Steps[1].StepText, Is.EqualTo("When my name is Tom Longhurst"));
            Assert.That(scenario.Steps[2].StepText, Is.EqualTo("Then I have a custom step 3"));
            
            Assert.That(JsonHelper.GetTestDynamicJsonObject().SelectToken("$.Scenarios[0].Steps[0].StepText").ToString(), Is.EqualTo("Given I have a custom step 1"));
            Assert.That(JsonHelper.GetTestDynamicJsonObject().SelectToken("$.Scenarios[0].Steps[1].StepText").ToString(), Is.EqualTo("When my name is Tom Longhurst"));
            Assert.That(JsonHelper.GetTestDynamicJsonObject().SelectToken("$.Scenarios[0].Steps[2].StepText").ToString(), Is.EqualTo("Then I have a custom step 3"));
        }
        
        [TestCase("Tom", "Longhurst")]
        public void CustomStepTexts_DynamicArgumentValue(string firstName, string lastName)
        {
            var scenario = Given(() => Action1Custom())
                .When(() => StepThatPrintsMyName(firstName, lastName))
                .Then(() => Action3Custom())
                .BDTest();
            
            Assert.That(scenario.Steps[0].StepText, Is.EqualTo("Given I have a custom step 1"));
            Assert.That(scenario.Steps[1].StepText, Is.EqualTo("When my name is Tom Longhurst"));
            Assert.That(scenario.Steps[2].StepText, Is.EqualTo("Then I have a custom step 3"));
            
            Assert.That(JsonHelper.GetTestDynamicJsonObject().SelectToken("$.Scenarios[0].Steps[0].StepText").ToString(), Is.EqualTo("Given I have a custom step 1"));
            Assert.That(JsonHelper.GetTestDynamicJsonObject().SelectToken("$.Scenarios[0].Steps[1].StepText").ToString(), Is.EqualTo("When my name is Tom Longhurst"));
            Assert.That(JsonHelper.GetTestDynamicJsonObject().SelectToken("$.Scenarios[0].Steps[2].StepText").ToString(), Is.EqualTo("Then I have a custom step 3"));
        }
        
        [Test]
        public void OverriddenStepTexts()
        {
            var text1 = "My first overridden steptext";
            var text2 = "My second overridden steptext";
            var text3 = "My third overridden steptext";
            
            var scenario = Given(() => Action1Custom()).WithStepText(() => text1)
                .When(() => StepThatPrintsMyName("Tom", "Longhurst")).WithStepText(() => text2)
                .Then(() => Action3Custom()).WithStepText(() => text3)
                .And(() => Action3Custom()).WithStepText(() => text3)
                .BDTest();
            
            Assert.That(scenario.Steps[0].StepText, Is.EqualTo("Given " + text1));
            Assert.That(scenario.Steps[1].StepText, Is.EqualTo("When " + text2));
            Assert.That(scenario.Steps[2].StepText, Is.EqualTo("Then " + text3));
            Assert.That(scenario.Steps[3].StepText, Is.EqualTo("And " + text3));
            
            Assert.That(JsonHelper.GetTestDynamicJsonObject().SelectToken("$.Scenarios[0].Steps[0].StepText").ToString(), Is.EqualTo("Given " + text1));
            Assert.That(JsonHelper.GetTestDynamicJsonObject().SelectToken("$.Scenarios[0].Steps[1].StepText").ToString(), Is.EqualTo("When " + text2));
            Assert.That(JsonHelper.GetTestDynamicJsonObject().SelectToken("$.Scenarios[0].Steps[2].StepText").ToString(), Is.EqualTo("Then " + text3));
            Assert.That(JsonHelper.GetTestDynamicJsonObject().SelectToken("$.Scenarios[0].Steps[3].StepText").ToString(), Is.EqualTo("And " + text3));
        }
        
        [Test]
        public void DefaultMethodNameStepTexts()
        {
            var scenario = Given(() => Step1WithoutAStepTextAttribute())
                .When(() => Step2WithoutAStepTextAttribute())
                .Then(() => Step_3_Without_A_StepTextAttribute_and_Hyphens())
                .BDTest();
            
            Assert.That(scenario.Steps[0].StepText, Is.EqualTo("Given step 1 without a step text attribute"));
            Assert.That(scenario.Steps[1].StepText, Is.EqualTo("When step 2 without a step text attribute"));
            Assert.That(scenario.Steps[2].StepText, Is.EqualTo("Then step 3 Without A StepTextAttribute and Hyphens"));
            
            Assert.That(JsonHelper.GetTestDynamicJsonObject().SelectToken("$.Scenarios[0].Steps[0].StepText").ToString(), Is.EqualTo("Given step 1 without a step text attribute"));
            Assert.That(JsonHelper.GetTestDynamicJsonObject().SelectToken("$.Scenarios[0].Steps[1].StepText").ToString(), Is.EqualTo("When step 2 without a step text attribute"));
            Assert.That(JsonHelper.GetTestDynamicJsonObject().SelectToken("$.Scenarios[0].Steps[2].StepText").ToString(), Is.EqualTo("Then step 3 Without A StepTextAttribute and Hyphens"));
        }
        
        [Test]
        public void FuncStepTextTest()
        {
            var scenario = Given(() => Console.WriteLine("I have a func that returns a strring"))
                    .When(() => FuncReturningStepText(() => "Blah"))
                    .Then(() => Console.WriteLine("the steptext should correctly have the func response"))
                    .BDTest();
            
            Assert.That(scenario.Steps[1].StepText, Is.EqualTo("When the func returns: Blah"));
        }
        
        [Test]
        public void WithStepTextList()
        {
            var scenario = Given(() => Console.WriteLine("Empty Step"))
                .When(() => Console.WriteLine("Empty Step"))
                .Then(() => StepWithListType(new List<string> {"Blah1", "Blah2", "Blah3"}))
                .BDTest();
            
            Assert.That(scenario.Steps[2].StepText, Is.EqualTo("Then the step text can display the list Blah1, Blah2, Blah3"));
        }
        
        [Test]
        public void WithCustomStepTextStringConverter()
        {
            BDTestSettings.CustomStringConverters.Add(new CustomClassToStepTextStringStringConverter());
            
            var scenario = Given(() => Console.WriteLine("Empty Step"))
                .When(() => Console.WriteLine("Empty Step"))
                .Then(() => StepWithStrongTypeAndStepText(new CustomClassToStepTextString()))
                .BDTest();
            
            Assert.That(scenario.Steps[2].StepText, Is.EqualTo("Then the text is 123..................4....................................5"));
        }
        
        [Test]
        public void NoStepTextIncludesParameterValues()
        {
            var scenario = When(() => StepWithParametersButWithoutStepText("One", "Two"))
                .Then(() => StepWithParametersButWithoutStepText("One", "Two"))
                .BDTest();
            
            Assert.That(scenario.Steps[1].StepText, Is.EqualTo("Then step with parameters but without step text One Two"));
        }

        [Test]
        public void StepTextForStrongTypeWithoutConvertor()
        {
            BDTestSettings.CustomStringConverters.RemoveAll(x => x.GetType() == typeof(CustomClassToStepTextStringStringConverter));
                        
            var scenario = Given(() => Console.WriteLine("Empty Step"))
                .When(() => Console.WriteLine("Empty Step"))
                .Then(() => StepWithStrongTypeAndStepText(new CustomClassToStepTextString
                {
                    One = "1", Two = "2", Three = "3", Four = "4", Five = "5"
                }))
                .BDTest();

            var thenStep = scenario.Steps.Last();
            
            Assert.That(thenStep.StepText, Is.EqualTo("Then the text is Out of the box ToString() called"));
        }
        
        [Test]
        public void StepTextForStrongTypeInInnerStepsClassWithoutConvertor()
        {
            BDTestSettings.CustomStringConverters.RemoveAll(x => x.GetType() == typeof(CustomClassToStepTextStringStringConverter));
                        
            var scenario = Given(() => Console.WriteLine("Empty Step"))
                .When(() => Console.WriteLine("Empty Step"))
                .Then(() => InnerSteps.StepWithStrongTypeAndStepText(new CustomClassToStepTextString
                {
                    One = "1", Two = "2", Three = "3", Four = "4", Five = "5"
                }))
                .BDTest();

            var thenStep = scenario.Steps.Last();
            
            Assert.That(thenStep.StepText, Is.EqualTo("Then the text is Out of the box ToString() called"));
        }
        
        [Test]
        public void StepTextForStrongTypeInInnerStepsWithoutStepTextAttribute()
        {
            BDTestSettings.CustomStringConverters.RemoveAll(x => x.GetType() == typeof(CustomClassToStepTextStringStringConverter));
                        
            var scenario = Given(() => Console.WriteLine("Empty Step"))
                .When(() => Console.WriteLine("Empty Step"))
                .Then(() => InnerSteps.StepWithStrongTypeAndNoStepText(new CustomClassToStepTextString
                {
                    One = "1", Two = "2", Three = "3", Four = "4", Five = "5"
                }))
                .BDTest();

            var thenStep = scenario.Steps.Last();
            
            Assert.That(thenStep.StepText, Is.EqualTo("Then step with strong type and no step text Out of the box ToString() called"));
        }

        private int _retryCount;
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [BDTestRetry(3)]
        public void RetrySuccessfully(int throwIfRetryLessThan)
        {
            var scenario = Given(() => InnerSteps.StepWithStrongTypeAndNoStepText(new CustomClassToStepTextString
                {
                    One = "1", Two = "2", Three = "3", Four = "4", Five = "5"
                }))
                .When(() => ThrowException(throwIfRetryLessThan))
                .Then(() => Console.WriteLine("the test passed"))
                .BDTest();
            
            var thenStep = scenario.Steps.First();
            
            Assert.That(thenStep.StepText, Is.EqualTo("Given step with strong type and no step text Out of the box ToString() called"));
        }
        
        private void ThrowException(int throwIfRetryLessThan)
        {
            if (_retryCount++ < throwIfRetryLessThan)
            {
                throw new MyCustomRetryException("Blah");
            }
        }
        
        [StepText("the text is {0}")]
        public void StepWithStrongTypeAndStepText(CustomClassToStepTextString obj)
        {
        }

        [StepText("the func returns: {0}")]
        public void FuncReturningStepText(Func<string> func)
        {
        }
        
        [StepText("the step text can display the list {0}")]
        public void StepWithListType(IList<string> list)
        {
        }

        [StepText("I have a custom step 1")]
        public void Action1Custom()
        {
        }
        
        [StepText("my name is {0} {1}")]
        public void StepThatPrintsMyName(string firstName, string lastName)
        {
        }
        
        [StepText("I have a custom step 3")]
        public void Action3Custom()
        {
        }
        
        public void Step1WithoutAStepTextAttribute()
        {
        }
        
        public void Step2WithoutAStepTextAttribute()
        {
        }
        
        public void Step_3_Without_A_StepTextAttribute_and_Hyphens()
        {
        }

        public void StepWithParametersButWithoutStepText(string valueOne, string valueTwo)
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
}