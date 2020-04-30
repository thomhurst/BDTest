using System;
using System.Linq;
using BDTest.Maps;
using BDTest.ReportGenerator;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests
{
    public class ExceptionTests : BDTestBase
    {
        [SetUp]
        public void Setup()
        {
            TestSetupHelper.ResetData();
        }

        [TestCase(typeof(Exception), Status.Failed)]
        [TestCase(typeof(NotImplementedException), Status.NotImplemented)]
        public void ThrowsExceptionOnGiven(Type exceptionType, Status status)
        {
            try
            {
                Given(() => ThrowException(exceptionType))  
                    .When(() => Console.WriteLine("my test has an exception"))
                    .Then(() => Console.WriteLine("the exception should be serialized to the json output"))
                    .And(() => Console.WriteLine("the step is marked as failed"))
                    .And(() => Console.WriteLine("the scenario is marked as failed"))
                    .And(() => Console.WriteLine("subsequent steps are inconclusive"))
                    .BDTest();
                
                Assert.Fail("An exception should be thrown to stop us getting here!");
            }
            catch
            {
                BDTestReportGenerator.Generate();

                var scenario = TestHolder.Scenarios.First();

                Assert.That(scenario.Steps[0].Exception.Message, Is.EqualTo("BDTest Exception!"));
            
                Assert.That(scenario.Steps[0].Status, Is.EqualTo(status));
                Assert.That(scenario.Steps[1].Status, Is.EqualTo(Status.Inconclusive));
                Assert.That(scenario.Steps[2].Status, Is.EqualTo(Status.Inconclusive));
                Assert.That(scenario.Steps[3].Status, Is.EqualTo(Status.Inconclusive));
                Assert.That(scenario.Steps[4].Status, Is.EqualTo(Status.Inconclusive));
                Assert.That(scenario.Steps[5].Status, Is.EqualTo(Status.Inconclusive));
            
                Assert.That(scenario.Status, Is.EqualTo(status));
            
                Assert.That(JsonHelper.GetDynamicJsonObject().SelectToken("$.Scenarios[0].Steps[0].Exception.Message").ToString(), Is.EqualTo("BDTest Exception!"));
            
                Assert.That(JsonHelper.GetDynamicJsonObject().SelectToken("$.Scenarios[0].Steps[0].Status").ToString(), Is.EqualTo(status.ToString()));
                Assert.That(JsonHelper.GetDynamicJsonObject().SelectToken("$.Scenarios[0].Steps[1].Status").ToString(), Is.EqualTo("Inconclusive"));
                Assert.That(JsonHelper.GetDynamicJsonObject().SelectToken("$.Scenarios[0].Steps[2].Status").ToString(), Is.EqualTo("Inconclusive"));
                Assert.That(JsonHelper.GetDynamicJsonObject().SelectToken("$.Scenarios[0].Steps[3].Status").ToString(), Is.EqualTo("Inconclusive"));
                Assert.That(JsonHelper.GetDynamicJsonObject().SelectToken("$.Scenarios[0].Steps[4].Status").ToString(), Is.EqualTo("Inconclusive"));
                Assert.That(JsonHelper.GetDynamicJsonObject().SelectToken("$.Scenarios[0].Steps[5].Status").ToString(), Is.EqualTo("Inconclusive"));
            
                Assert.That(JsonHelper.GetDynamicJsonObject().SelectToken("$.Scenarios[0].Status").ToString(), Is.EqualTo(status.ToString()));
            }
        }
        
        [TestCase(typeof(Exception), Status.Failed)]
        [TestCase(typeof(NotImplementedException), Status.NotImplemented)]
        public void ThrowsExceptionOnThen(Type exceptionType, Status status)
        {
            try
            {
                When(() => Console.WriteLine("my test has an exception"))
                    .Then(() => Console.WriteLine("the exception should be serialized to the json output"))
                    .And(() => Console.WriteLine("the step is marked as failed"))
                    .And(() => ThrowException(exceptionType))
                    .And(() => Console.WriteLine("subsequent steps are inconclusive"))
                    .And(() => Console.WriteLine("Previous steps should be marked as passed"))
                    .BDTest();
                
                Assert.Fail("An exception should be thrown to stop us getting here!");
            }
            catch
            {
                BDTestReportGenerator.Generate();

                var scenario = TestHolder.Scenarios.First();

                Assert.That(scenario.Steps[3].Exception.Message, Is.EqualTo("BDTest Exception!"));
            
                Assert.That(scenario.Steps[0].Status, Is.EqualTo(Status.Passed));
                Assert.That(scenario.Steps[1].Status, Is.EqualTo(Status.Passed));
                Assert.That(scenario.Steps[2].Status, Is.EqualTo(Status.Passed));
                Assert.That(scenario.Steps[3].Status, Is.EqualTo(status));
                Assert.That(scenario.Steps[4].Status, Is.EqualTo(Status.Inconclusive));
                Assert.That(scenario.Steps[5].Status, Is.EqualTo(Status.Inconclusive));

                Assert.That(scenario.Status, Is.EqualTo(status));
            
                Assert.That(JsonHelper.GetDynamicJsonObject().SelectToken("$.Scenarios[0].Steps[3].Exception.Message").ToString(), Is.EqualTo("BDTest Exception!"));
            
                Assert.That(JsonHelper.GetDynamicJsonObject().SelectToken("$.Scenarios[0].Steps[0].Status").ToString(), Is.EqualTo("Passed"));
                Assert.That(JsonHelper.GetDynamicJsonObject().SelectToken("$.Scenarios[0].Steps[1].Status").ToString(), Is.EqualTo("Passed"));
                Assert.That(JsonHelper.GetDynamicJsonObject().SelectToken("$.Scenarios[0].Steps[2].Status").ToString(), Is.EqualTo("Passed"));
                Assert.That(JsonHelper.GetDynamicJsonObject().SelectToken("$.Scenarios[0].Steps[3].Status").ToString(), Is.EqualTo(status.ToString()));
                Assert.That(JsonHelper.GetDynamicJsonObject().SelectToken("$.Scenarios[0].Steps[4].Status").ToString(), Is.EqualTo("Inconclusive"));
                Assert.That(JsonHelper.GetDynamicJsonObject().SelectToken("$.Scenarios[0].Steps[5].Status").ToString(), Is.EqualTo("Inconclusive"));

                Assert.That(JsonHelper.GetDynamicJsonObject().SelectToken("$.Scenarios[0].Status").ToString(), Is.EqualTo(status.ToString()));
            }
        }

        private static void ThrowException(Type exceptionType)
        {
            var exception = (Exception) Activator.CreateInstance(exceptionType, "BDTest Exception!");
            throw exception;
        }
    }
}