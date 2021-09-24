using System;
using System.Threading.Tasks;
using BDTest.Attributes;
using NUnit.Framework;

namespace BDTest.Example
{
    [Story(AsA = "BDTest Developer",
        IWant = "to show how different states show in a test report",
        SoThat = "users can see what a report would look like")]
    public class DifferentReportScenarioTests : MyTestBase
    {
        [Test]
        [ScenarioText("An ignored test")]
        public async Task TestIgnored()
        {
            await When(() => Ignore())
                .Then(() => Pass())
                .BDTestAsync();
        }
        
        [Test]
        [ScenarioText("An inconclusive given test")]
        public async Task TestGivenInconclusive()
        {
            await Given(() => Inconclusive()) 
                .When(() => Pass())
                .Then(() => Pass())
                .BDTestAsync();
        }
        
        [Test]
        [ScenarioText("An inconclusive when test")]
        public async Task TestWhenInconclusive()
        {
            await When(() => Inconclusive())
                .Then(() => Pass())
                .BDTestAsync();
        }
        
        [Test]
        [ScenarioText("An inconclusive then test")]
        public async Task TestThenInconclusive()
        {
            await When(() => Pass())
                .Then(() => Inconclusive())
                .BDTestAsync();
        }

        [Test]
        [ScenarioText("The given step failed")]
        public async Task GivenFailed()
        {
            await Given(() => Fail()) 
                .When(() => Pass())
                .Then(() => Pass())
                .BDTestAsync();
        }

        [Test]
        [ScenarioText("The and given step failed")]
        public async Task AndGivenFailed()
        {
            await Given(() => Pass())
                .And(() => Fail())
                .When(() => Pass())
                .Then(() => Pass())
                .BDTestAsync();
        }

        [Test]
        [ScenarioText("The when step failed")]
        public async Task WhenFailed()
        {
            await When(() => Fail())
                .Then(() => Pass())
                .BDTestAsync();
        }

        [Test]
        [ScenarioText("The then step failed")]
        public async Task ThenFailed()
        {
            await When(() => Pass())
                .Then(() => Fail())
                .BDTestAsync();
        }

        [Test]
        [ScenarioText("The and then step failed")]
        public async Task AndThenFailed()
        {
            await When(() => Pass())
                .Then(() => Pass())
                .And(() => Fail())
                .BDTestAsync();
        }

        [StepText("the step passes")]
        private void Pass()
        {
        }

        [StepText("I fail the step")]
        private void Fail()
        {
            throw new Exception();
        }

        private void Ignore()
        {
            Assert.Ignore();
        }

        private void Inconclusive()
        {
            Assert.Inconclusive();
        }
    }
}