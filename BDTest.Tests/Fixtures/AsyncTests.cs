using System;
using System.Diagnostics;
using System.Threading.Tasks;
using BDTest.Attributes;
using BDTest.Test;
using BDTest.Tests.Extensions;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures
{
    [Parallelizable(ParallelScope.All)]
    [Story(AsA = "BDTest developer",
        IWant = "to make sure that BDTest works asynchronously",
        SoThat = "tests run with maxiumum performance")]
    public class AsyncTests : BDTestBase
    {
        [Test]
        public void BlockingTest()
        {
            var stopwatch = Stopwatch.StartNew();
            
            var scenario = Given(() => Task.Delay(1250))
                .When(() => Task.Delay(1250))
                .Then(() => Task.Delay(1250))
                .BDTest();

            Assert.That(stopwatch.StopAndGetElapsed().Seconds, Is.GreaterThanOrEqualTo(3));
            Assert.That(scenario.TimeTaken > TimeSpan.FromSeconds(3));
        }
        
        [Test]
        public async Task AwaitedTest()
        {
            var stopwatch = Stopwatch.StartNew();
            
            var scenario = await Given(() => Task.Delay(1250))
                .When(() => Task.Delay(1250))
                .Then(() => Task.Delay(1250))
                .BDTestAsync();

            Assert.That(stopwatch.StopAndGetElapsed().Seconds, Is.GreaterThanOrEqualTo(3));
            Assert.That(scenario.TimeTaken > TimeSpan.FromSeconds(3));
        }
        
        [Test]
        public async Task AwaitedOnThenStep()
        {
            var stopwatch = Stopwatch.StartNew();
            
            var scenario = await Given(() => Task.Delay(1250))
                .When(() => Task.Delay(1250))
                .Then(() => Task.Delay(1250));

            Assert.That(stopwatch.StopAndGetElapsed().Seconds, Is.GreaterThanOrEqualTo(3));
            Assert.That(scenario.TimeTaken > TimeSpan.FromSeconds(3));
        }
    }
}