using System;
using System.Linq;
using BDTest.Attributes;
using BDTest.Test;
using BDTest.Tests.Helpers;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures
{
    [Parallelizable(ParallelScope.None)]
    [Story(AsA = "BDTest developer",
        IWant = "to make sure that BDTest flags tests not executed",
        SoThat = "consumers aren't missing any test coverage accidentally")]
    public class NotRunWarnings : BDTestBase
    {
        [OneTimeSetUp]
        public void Setup()
        {
            TestResetHelper.ResetData();
        }

        [Test, Order(4)]
        public void Test()
        {
            SuccessTest();

            NotRunTest1();

            NotRunTest2();

            NotRunTest3();

            var notRunScenarios = BDTestUtil.GetNotRunScenarios();
            Assert.That(notRunScenarios.Count, Is.EqualTo(3));

            Assert.That(notRunScenarios.Any(scenario => scenario.GetScenarioText() == "Custom NotRunTest1"));
            Assert.That(notRunScenarios.Any(scenario => scenario.GetScenarioText() == "Custom NotRunTest2"));
            Assert.That(notRunScenarios.Any(scenario => scenario.GetScenarioText() == "Custom NotRunTest3"));

            Assert.That(notRunScenarios.All(scenario => scenario.GetStoryText() == @"As a BDTest developer
I want to make sure that BDTest flags tests not executed
So that consumers aren't missing any test coverage accidentally
"));
        }

        [ScenarioText("Custom NotRunTest3")]
        private void NotRunTest3()
        {
            When(() => Console.WriteLine("Don't execute a test properly"))
                .Then(() => Console.WriteLine("It shows as a warning in the report."))
                .And(() => Console.WriteLine("I know that the test is"));
        }

        [ScenarioText("Custom NotRunTest2")]
        private void NotRunTest2()
        {
            When(() => Console.WriteLine("Don't execute a test properly"))
                .Then(() => Console.WriteLine("It shows as a warning in the report."));
        }

        [ScenarioText("Custom NotRunTest1")]
        private void NotRunTest1()
        {
            When(() => Console.WriteLine("Don't execute a test properly"));
        }

        [ScenarioText("Success Test")]
        private void SuccessTest()
        {
            When(() => Console.WriteLine("Run a test successfully"))
                .Then(() => Console.WriteLine("It shows in the report successfully."))
                .BDTest();
        }
    }
}