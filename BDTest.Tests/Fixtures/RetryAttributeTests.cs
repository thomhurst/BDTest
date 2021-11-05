using System;
using System.Linq;
using BDTest.Attributes;
using BDTest.Test;
using BDTest.Test.Steps.When;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures
{
    public class RetryAttributeTests
    {
        public class RetryOnScenarioTests : BDTestBase
        {
            private int _counter;

            [SetUp, BDTestRetrySetUp]
            public void Setup()
            {
                _counter++;
            }

            private int testRetryCount = 0;
            [Test, ScenarioText("RetryOnScenarioTests"), BDTestRetry(3)]
            public void Test()
            {
                Given(() => Console.WriteLine("I run a test with a retry attribute"))
                    .And(() => IncrementCount(ref testRetryCount))
                    .When(() => ThrowIfLessThan(testRetryCount))
                    .Then(() => Assert.That(_counter, Is.EqualTo(testRetryCount)))
                    .And(() => Assert.That(_counter, Is.Positive))
                    .And(() => Assert.That(testRetryCount, Is.Positive))
                    .And(() => Console.WriteLine($"Counter: {_counter} | TestRetryCount: {testRetryCount}"))
                    .BDTest();
            }

            private void ThrowIfLessThan(int i)
            {
                if (i < 3)
                {
                    throw new Exception();
                }
            }

            [OneTimeTearDown]
            public void OneTimeTearDown()
            {
                var scenarios = BDTestUtil.GetScenarios().Where(x => x.GetScenarioText() == "RetryOnScenarioTests").ToList();
                Assert.That(scenarios.Count, Is.EqualTo(1));
                Assert.That(scenarios.First().Status, Is.EqualTo(Status.Passed));
            }

            private void IncrementCount(ref int i)
            {
                i++;
            }
        }
        
        [BDTestRetry(3)]
        public class RetryOnStoryTests : BDTestBase
        {
            private int _counter;

            [SetUp, BDTestRetrySetUp]
            public void Setup()
            {
                _counter++;
            }

            private int testRetryCount = 0;
            [Test, ScenarioText("RetryOnStoryTests")]
            public void Test()
            {
                Given(() => Console.WriteLine("I run a test with a retry attribute"))
                    .And(() => IncrementCount(ref testRetryCount))
                    .When(() => ThrowIfLessThan(testRetryCount))
                    .Then(() => Assert.That(_counter, Is.EqualTo(testRetryCount)))
                    .And(() => Assert.That(_counter, Is.Positive))
                    .And(() => Assert.That(testRetryCount, Is.Positive))
                    .And(() => Console.WriteLine($"Counter: {_counter} | TestRetryCount: {testRetryCount}"))
                    .BDTest();
            }

            private void ThrowIfLessThan(int i)
            {
                if (i < 3)
                {
                    throw new Exception();
                }
            }

            [OneTimeTearDown]
            public void OneTimeTearDown()
            {
                var scenarios = BDTestUtil.GetScenarios().Where(x => x.GetScenarioText() == "RetryOnStoryTests").ToList();
                Assert.That(scenarios.Count, Is.EqualTo(1));
                Assert.That(scenarios.First().Status, Is.EqualTo(Status.Passed));
            }

            private void IncrementCount(ref int i)
            {
                i++;
            }
        }
    }
}