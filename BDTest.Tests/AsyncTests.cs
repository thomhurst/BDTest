using System.Diagnostics;
using System.Threading.Tasks;
using BDTest.ReportGenerator;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests
{
    [Parallelizable(ParallelScope.Children)]
    public class AsyncTests : BDTestBase
    {
        [Test]
        public void BlockingTest()
        {
            var stopwatch = Stopwatch.StartNew();
            
            Given(() => Task.Delay(1000))
                .When(() => Task.Delay(1000))
                .Then(() => Task.Delay(1000))
                .BDTest();
            
            Assert.That(stopwatch.Elapsed.Seconds, Is.GreaterThanOrEqualTo(3));
            
            BDTestReportGenerator.Generate();
        }
        
        [Test]
        public async Task AwaitedTest()
        {
            var stopwatch = Stopwatch.StartNew();
            
            await Given(() => Task.Delay(1000))
                .When(() => Task.Delay(1000))
                .Then(() => Task.Delay(1000))
                .BDTestAsync();
            
            Assert.That(stopwatch.Elapsed.Seconds, Is.GreaterThanOrEqualTo(3));
        }
        
        [Test]
        public async Task AwaitedOnThenStep()
        {
            var stopwatch = Stopwatch.StartNew();
            
            await Given(() => Task.Delay(1000))
                .When(() => Task.Delay(1000))
                .Then(() => Task.Delay(1000));
            
            Assert.That(stopwatch.Elapsed.Seconds, Is.GreaterThanOrEqualTo(3));
        }
    }
}