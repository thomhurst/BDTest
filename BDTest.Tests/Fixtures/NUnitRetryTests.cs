using System;
using BDTest.Attributes;
using BDTest.NUnit;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures
{
    public class NUnitRetryTests : NUnitBDTestBase<MyTestContext>
    {
        private int _testRetryCount = 0;
        [Test, ScenarioText("NUnitRetryOnScenarioTests"), BDTestRetry(3)]
        public void Test()
        {
            Given(() => Console.WriteLine("I run a test with a retry attribute"))
                .And(() => IncrementCount(ref _testRetryCount))
                .When(() => ThrowIfLessThan(_testRetryCount))
                .Then(() => Console.WriteLine($"TestRetryCount: {_testRetryCount}"))
                .BDTest();
        }
        
        private void IncrementCount(ref int i)
        {
            i++;
        }
        
        private void ThrowIfLessThan(int i)
        {
            Assert.That(i, Is.GreaterThanOrEqualTo(3));
        }
    }
}