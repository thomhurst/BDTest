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
        IWant = "to make sure that certain steps can be skipped conditionally",
        SoThat = "tests can be run with different behaviour for different test environments")]
    public class SkipCertainStepsTests : BDTestBase
    {
        [OneTimeSetUp]
        public void Setup()
        {
            BDTestSettings.SkipStepRules.Add<SkipAttribute1>(attr => true);
            BDTestSettings.SkipStepRules.Add<SkipAttribute2>(attr => true);
            BDTestSettings.SkipStepRules.Add<SkipAttribute3>(attr => true);
            BDTestSettings.SkipStepRules.Add<SkipAttributeWithParameter>(attr => attr.ShouldSkip);
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
        public void ShouldSkipTrue()
        {
            Given(() => Console.WriteLine("A test"))
                .When(() => Console.WriteLine())
                .Then(() => Console.WriteLine("The test passes because the exception step was skipped"))
                .And(() => ThrowExceptionShouldSkipTrue())
                .BDTest();
        }
        
        [Test]
        public void ShouldSkipFalse()
        {
            try
            {
                Given(() => Console.WriteLine("A test"))
                    .When(() => Console.WriteLine())
                    .Then(() => Console.WriteLine("The test passes because the exception step was skipped"))
                    .And(() => ThrowExceptionShouldSkipFalse())
                    .BDTest();
                
                Assert.Fail("The last exception should not be skipped - And should stop us getting here.");
            }
            catch (Exception e) when(e.Message == "ThrowExceptionShouldSkipFalse")
            {
                Assert.Pass();
            }
            
            Assert.Fail();
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
        
        [SkipAttributeWithParameter(true)]
        private static Task ThrowExceptionShouldSkipTrue()
        {
            throw new Exception("ThrowExceptionShouldSkipTrue");
        }
        
        [SkipAttributeWithParameter(false)]
        private static Task ThrowExceptionShouldSkipFalse()
        {
            throw new Exception("ThrowExceptionShouldSkipFalse");
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
    
    class SkipAttributeWithParameter : SkipStepAttribute
    {
        public bool ShouldSkip { get; }

        public SkipAttributeWithParameter(bool shouldSkip)
        {
            ShouldSkip = shouldSkip;
        }
    }
    
    class NotRegisteredSkipAttribute : SkipStepAttribute
    {
        
    }
}