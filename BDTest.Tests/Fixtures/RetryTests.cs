using System;
using System.Threading.Tasks;
using BDTest.Attributes;
using BDTest.Settings;
using BDTest.Test;
using BDTest.Tests.Helpers;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures
{
    [Parallelizable(ParallelScope.None)]
    [Story(AsA = "BDTest developer",
        IWant = "to make sure that tests can be retried",
        SoThat = "developers can mitigate against flakey tests")]
    public class RetryTests : BDTestBase
    {
        private int _retryMethodCallCount;
        
        private int _setUpCounter = 0;
        private int _tearDownCounter = 0;
        
        [BDTestRetrySetUp]
        public async Task BDTestRetrySetup()
        {
            await Task.Delay(500);
            _setUpCounter++;
        }
        
        [BDTestRetryTearDown]
        public async Task BDTestRetryTearDown()
        {
            await Task.Delay(500);
            _tearDownCounter++;
        }
        
        [SetUp]
        public void Setup()
        {
            _retryCount = 0;
            _retryMethodCallCount = 0;
            TestResetHelper.ResetData();
        }
        
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            BDTestSettings.RetryTestRules.Add(exception => exception is MyCustomRetryException, 3);
        }

        private int _retryCount;

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void RetrySuccessfully(int throwIfRetryLessThan)
        {
            var scenario = Given(() => ThrowException(throwIfRetryLessThan))
                .When(() => Console.WriteLine("my test has an exception"))
                .Then(() => Console.WriteLine("the test should retry"))
                .BDTest();
            
            Assert.That(scenario.RetryCount, Is.EqualTo(throwIfRetryLessThan));
            Assert.That(_retryMethodCallCount, Is.EqualTo(throwIfRetryLessThan));
            
            Assert.That(_setUpCounter, Is.Positive);
            Assert.That(_tearDownCounter, Is.Positive);
        }

        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void RetryPastLimit(int throwIfRetryLessThan)
        {
            try
            {
                Given(() => ThrowException(throwIfRetryLessThan))
                    .When(() => Console.WriteLine("my test has an exception"))
                    .Then(() => Console.WriteLine("the test should retry"))
                    .BDTest();

                Assert.Fail();
            }
            catch (MyCustomRetryException)
            {
                Assert.That(_retryMethodCallCount, Is.EqualTo(3));
                Assert.Pass();
            }
        }

        private void ThrowException(int throwIfRetryLessThan)
        {
            if (_retryCount++ < throwIfRetryLessThan)
            {
                throw new MyCustomRetryException("Blah");
            }
        }

        public override Task OnRetry()
        {
            _retryMethodCallCount++;
            return Task.CompletedTask;
        }
    }

    public class MyCustomRetryException : Exception
    {
        public MyCustomRetryException(string message) : base(message)
        {
        }
    }
}