using System;
using System.Diagnostics;
using System.Threading.Tasks;
using BDTest.ReportGenerator;
using BDTest.Test;
using BDTest.Tests.Extensions;
using BDTest.Tests.Helpers;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures
{
    [Parallelizable(ParallelScope.None)]
    public class AsyncTests : BDTestBase
    {
        [Test]
        public void BlockingTest()
        {
            var stopwatch = Stopwatch.StartNew();
            
            var scenario = Given(() => Task.Delay(1000))
                .When(() => Task.Delay(1000))
                .Then(() => Task.Delay(1000))
                .BDTest();

            Assert.That(stopwatch.StopAndGetElapsed().Seconds, Is.GreaterThanOrEqualTo(3));
            Assert.That(scenario.TimeTaken > TimeSpan.FromSeconds(3));
            
            BDTestReportGenerator.GenerateInFolder(FileHelpers.GetUniqueTestOutputFolder());
        }
        
        [Test]
        public async Task AwaitedTest()
        {
            var stopwatch = Stopwatch.StartNew();
            
            var scenario = await Given(() => Task.Delay(1000))
                .When(() => Task.Delay(1000))
                .Then(() => Task.Delay(1000))
                .BDTestAsync();

            Assert.That(stopwatch.StopAndGetElapsed().Seconds, Is.GreaterThanOrEqualTo(3));
            Assert.That(scenario.TimeTaken > TimeSpan.FromSeconds(3));
            
            BDTestReportGenerator.GenerateInFolder(FileHelpers.GetUniqueTestOutputFolder());
        }
        
        [Test]
        public async Task AwaitedOnThenStep()
        {
            var stopwatch = Stopwatch.StartNew();
            
            var scenario = await Given(() => Task.Delay(1000))
                .When(() => Task.Delay(1000))
                .Then(() => Task.Delay(1000));

            Assert.That(stopwatch.StopAndGetElapsed().Seconds, Is.GreaterThanOrEqualTo(3));
            Assert.That(scenario.TimeTaken > TimeSpan.FromSeconds(3));
            
            BDTestReportGenerator.GenerateInFolder(FileHelpers.GetUniqueTestOutputFolder());
        }
    }
}