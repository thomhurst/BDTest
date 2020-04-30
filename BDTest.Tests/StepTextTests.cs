using BDTest.Attributes;
using BDTest.ReportGenerator;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests
{
    [Parallelizable(ParallelScope.None)]
    public class StepTextTests : BDTestBase
    {
        [SetUp]
        public void Setup()
        {
            TestSetupHelper.ResetData();
        }

        [Test]
        public void CustomStepTexts()
        {
            var scenario = Given(() => Action1Custom())
                .When(() => StepThatPrintsMyName("Tom", "Longhurst"))
                .Then(() => Action3Custom())
                .BDTest();
            
            BDTestReportGenerator.Generate();

            Assert.That(scenario.Steps[0].StepText, Is.EqualTo("Given I have a custom step 1"));
            Assert.That(scenario.Steps[1].StepText, Is.EqualTo("When my name is Tom Longhurst"));
            Assert.That(scenario.Steps[2].StepText, Is.EqualTo("Then I have a custom step 3"));
            
            Assert.That(JsonHelper.GetDynamicJsonObject().SelectToken("$.Scenarios[0].Steps[0].StepText").ToString(), Is.EqualTo("Given I have a custom step 1"));
            Assert.That(JsonHelper.GetDynamicJsonObject().SelectToken("$.Scenarios[0].Steps[1].StepText").ToString(), Is.EqualTo("When my name is Tom Longhurst"));
            Assert.That(JsonHelper.GetDynamicJsonObject().SelectToken("$.Scenarios[0].Steps[2].StepText").ToString(), Is.EqualTo("Then I have a custom step 3"));
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
            
            BDTestReportGenerator.Generate();

            Assert.That(scenario.Steps[0].StepText, Is.EqualTo("Given " + text1));
            Assert.That(scenario.Steps[1].StepText, Is.EqualTo("When " + text2));
            Assert.That(scenario.Steps[2].StepText, Is.EqualTo("Then " + text3));
            Assert.That(scenario.Steps[3].StepText, Is.EqualTo("And " + text3));
            
            Assert.That(JsonHelper.GetDynamicJsonObject().SelectToken("$.Scenarios[0].Steps[0].StepText").ToString(), Is.EqualTo("Given " + text1));
            Assert.That(JsonHelper.GetDynamicJsonObject().SelectToken("$.Scenarios[0].Steps[1].StepText").ToString(), Is.EqualTo("When " + text2));
            Assert.That(JsonHelper.GetDynamicJsonObject().SelectToken("$.Scenarios[0].Steps[2].StepText").ToString(), Is.EqualTo("Then " + text3));
            Assert.That(JsonHelper.GetDynamicJsonObject().SelectToken("$.Scenarios[0].Steps[3].StepText").ToString(), Is.EqualTo("And " + text3));
        }
        
        [Test]
        public void DefaultMethodNameStepTexts()
        {
            var scenario = Given(() => Step1WithoutAStepTextAttribute())
                .When(() => Step2WithoutAStepTextAttribute())
                .Then(() => Step_3_Without_A_StepTextAttribute_and_Hyphens())
                .BDTest();
            
            BDTestReportGenerator.Generate();

            Assert.That(scenario.Steps[0].StepText, Is.EqualTo("Given Step 1 without a step text attribute"));
            Assert.That(scenario.Steps[1].StepText, Is.EqualTo("When Step 2 without a step text attribute"));
            Assert.That(scenario.Steps[2].StepText, Is.EqualTo("Then Step 3 Without A StepTextAttribute and Hyphens"));
            
            Assert.That(JsonHelper.GetDynamicJsonObject().SelectToken("$.Scenarios[0].Steps[0].StepText").ToString(), Is.EqualTo("Given Step 1 without a step text attribute"));
            Assert.That(JsonHelper.GetDynamicJsonObject().SelectToken("$.Scenarios[0].Steps[1].StepText").ToString(), Is.EqualTo("When Step 2 without a step text attribute"));
            Assert.That(JsonHelper.GetDynamicJsonObject().SelectToken("$.Scenarios[0].Steps[2].StepText").ToString(), Is.EqualTo("Then Step 3 Without A StepTextAttribute and Hyphens"));
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
    }
}