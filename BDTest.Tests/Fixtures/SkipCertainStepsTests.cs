using System;
using System.Threading.Tasks;
using BDTest.Attributes;
using BDTest.ReportGenerator;
using BDTest.Settings;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures
{
    public class SkipCertainStepsTests : BDTestBase
    {
        [OneTimeSetUp]
        public void Setup()
        {
            BDTestSettings.SkipStepRules.Add<SkipAttribute1>(() => true);
            BDTestSettings.SkipStepRules.Add<SkipAttribute2>(() => true);
            BDTestSettings.SkipStepRules.Add<SkipAttribute3>(() => true);
        }

        [OneTimeTearDown]
        public void Teardown()
        {
            BDTestReportGenerator.GenerateInFolder(nameof(SkipCertainStepsTests));
        }

        [Test]
        public void Skip1Step()
        {
            Given(() => Console.WriteLine("A test"))
                .When(() => Console.WriteLine())
                .Then(() => Console.WriteLine("The test passes because the exception step was skipped"))
                .And(() => ThrowException1())
                .BDTest();
        }
        
        [Test]
        public void Skip3Steps()
        {
            Given(() => Console.WriteLine("A test"))
                .When(() => Console.WriteLine())
                .Then(() => Console.WriteLine("The test passes because the exception step was skipped"))
                .And(() => ThrowException1())
                .And(() => ThrowException2())
                .And(() => ThrowException3())
                .BDTest();
        }
        
        [Test]
        public void NotRegisteredSkipException()
        {
            try
            {
                Given(() => Console.WriteLine("A test"))
                    .When(() => Console.WriteLine())
                    .Then(() => Console.WriteLine("The test passes because the exception step was skipped"))
                    .And(() => ThrowException1())
                    .And(() => ThrowException2())
                    .And(() => ThrowException3())
                    .And(() => ThrowNotRegisteredSkipException())
                    .BDTest();
                
                Assert.Fail("The last exception should not be skipped - And should stop us getting here.");
            }
            catch (Exception e) when(e.Message == "ThrowNotRegisteredSkipException")
            {
                Assert.Pass();
            }
            
            Assert.Fail();
        }

        [NotRegisteredSkip]
        private void ThrowNotRegisteredSkipException()
        {
            throw new Exception("ThrowNotRegisteredSkipException");
        }

        [SkipAttribute1]
        private static Task ThrowException1()
        {
            throw new Exception("ThrowException1");
        }
        
        [SkipAttribute2]
        private static Task ThrowException2()
        {
            throw new Exception("ThrowException2");
        }
        
        [SkipAttribute3]
        private static Task ThrowException3()
        {
            throw new Exception("ThrowException3");
        }
    }

    class SkipAttribute1 : SkipStepAttribute
    {
        
    }
    
    class SkipAttribute2 : SkipStepAttribute
    {
        
    }
    
    class SkipAttribute3 : SkipStepAttribute
    {
        
    }
    
    class NotRegisteredSkipAttribute : SkipStepAttribute
    {
        
    }
}