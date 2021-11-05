using System;
using System.Threading.Tasks;
using BDTest.Attributes;
using BDTest.ReportGenerator;
using BDTest.Settings;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures
{
    [Story(AsA = "BDTest developer",
        IWant = "to make sure that certain tests can be skipped conditionally",
        SoThat = "tests can be run with different behaviour for different test environments")]
    public class SkipCertainTestsTests : BDTestBase
    {
        [OneTimeSetUp]
        public void Setup()
        {
            BDTestSettings.GlobalSkipTestRules.Add(test => test.BdTestBaseClass?.GetType() == typeof(SkipCertainTestsTests));
        }

        [OneTimeTearDown]
        public void Teardown()
        {
            BDTestReportGenerator.GenerateInFolder(nameof(SkipCertainTestsTests));
        }

        [Test]
        public void Skip1()
        {
            var test = Given(() => Console.WriteLine("A test"))
                .When(() => Console.WriteLine())
                .Then(() => Console.WriteLine("The test passes because the exception step was skipped"))
                .And(() => ThrowException1())
                .BDTest();
            
            Assert.That(test.Status, Is.EqualTo(Status.Skipped));
        }
        
        [Test]
        public void Skip2()
        {
            var test = Given(() => Console.WriteLine("A test"))
                .When(() => Console.WriteLine())
                .Then(() => Console.WriteLine("The test passes because the exception step was skipped"))
                .And(() => ThrowException1())
                .And(() => ThrowException1())
                .And(() => ThrowException1())
                .BDTest();
            
            Assert.That(test.Status, Is.EqualTo(Status.Skipped));
        }

        [Test]
        public void Skip3()
        {
            var test = Given(() => Console.WriteLine("A test"))
                .When(() => Console.WriteLine())
                .Then(() => Console.WriteLine("The test passes because the exception step was skipped"))
                .And(() => ThrowException1())
                .BDTest();
            
            Assert.That(test.Status, Is.EqualTo(Status.Skipped));
        }

        private void ThrowException1()
        {
            throw new Exception();
        }
    }
}