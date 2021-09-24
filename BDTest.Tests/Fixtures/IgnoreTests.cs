using System;
using System.Linq;
using BDTest.Attributes;
using BDTest.NUnit;
using BDTest.Settings;
using BDTest.Test;
using BDTest.Tests.Helpers;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures
{
    [Story(AsA = "BDTest developer",
        IWant = "to make sure that specific exceptions can be marked as 'skippable'",
        SoThat = "BDTest works well with other frameworks such as NUnit")]
    [Parallelizable(ParallelScope.None)]
    public class IgnoreTests : NUnitBDTestBase<MyTestContext>
    {
        [SetUp]
        public void Setup()
        {
            TestResetHelper.ResetData();
        }
        
        [Test]
        public void NUNitIgnoreException()
        {
            try
            {
                Given(() => Console.WriteLine("NUnit throws an ignore exception"))
                    .When(() => Assert.Ignore())
                    .Then(() => Console.WriteLine("the test is ignored"))
                    .BDTest();
                
                Assert.Fail("An exception should be thrown to stop us getting here!");
            }
            catch(IgnoreException)
            {
                var scenario = BDTestUtil.GetScenarios().First();
                
                Assert.That(scenario.Status, Is.EqualTo(Status.Inconclusive));
            }
        }
        
        [Test]
        public void CustomIgnoreException()
        {
            BDTestSettings.CustomExceptionSettings.InconclusiveExceptionTypes.Add(typeof(MyCustomIgnoreException));
            try
            {
                Given(() => Console.WriteLine("NUnit throws an ignore exception"))
                    .When(() => ThrowException())
                    .Then(() => Console.WriteLine("the test is ignored"))
                    .BDTest();
                
                Assert.Fail("An exception should be thrown to stop us getting here!");
            }
            catch(MyCustomIgnoreException)
            {
                var scenario = BDTestUtil.GetScenarios().First();
                
                Assert.That(scenario.Status, Is.EqualTo(Status.Inconclusive));
            }
        }

        private void ThrowException()
        {
            throw new MyCustomIgnoreException();
        }
    }

    public class MyCustomIgnoreException : Exception
    {
    }
}