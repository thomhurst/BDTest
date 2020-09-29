using System;
using System.Threading.Tasks;
using BDTest.Attributes;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures
{
    [Parallelizable(ParallelScope.None)]
    [Story(AsA = "BDTest developer",
        IWant = "to make sure that I can intercept console output",
        SoThat = "tests contain useful information")]
    public class InterceptConsoleOutputTests : BDTestBase
    {
        // Async - .BDTestAsync()
        [Test]
        [Repeat(10)]
        public async Task InterceptConsoleOutput_Synchronous_Steps_Called_Asynchronously()
        {
            var scenario = await When(() => Console.WriteLine("blah output when"))
                .Then(() => Console.WriteLine("blah output then"))
                .And(() => Console.WriteLine("blah output and then"))
                .BDTestAsync();

            Assert.That(scenario.Steps[0].Output, Is.EqualTo("blah output when"));
            Assert.That(scenario.Steps[1].Output, Is.EqualTo("blah output then"));
            Assert.That(scenario.Steps[2].Output, Is.EqualTo("blah output and then"));
        }
        
        [Test]
        [Repeat(10)]
        public async Task InterceptConsoleOutput_Asynchronous_Steps_Called_Asynchronously()
        {
            var scenario = await When(() => Console.Out.WriteLineAsync("blah output when"))
                .Then(() => Console.Out.WriteLineAsync("blah output then"))
                .And(() => Console.Out.WriteLineAsync("blah output and then"))
                .BDTestAsync();
            
            Assert.That(scenario.Steps[0].Output, Is.EqualTo("blah output when"));
            Assert.That(scenario.Steps[1].Output, Is.EqualTo("blah output then"));
            Assert.That(scenario.Steps[2].Output, Is.EqualTo("blah output and then"));
        }
        
        [Test]
        [Repeat(10)]
        public async Task InterceptConsoleOutput_Synchronous_And_Asynchronous_Steps_Mix1_Called_Asynchronously()
        {
            var scenario = await When(() => Console.Out.WriteLineAsync("blah output when"))
                .Then(() => Console.WriteLine("blah output then"))
                .And(() => Console.Out.WriteLineAsync("blah output and then"))
                .BDTestAsync();

            Assert.That(scenario.Steps[0].Output, Is.EqualTo("blah output when"));
            Assert.That(scenario.Steps[1].Output, Is.EqualTo("blah output then"));
            Assert.That(scenario.Steps[2].Output, Is.EqualTo("blah output and then"));
        }
        
        [Test]
        [Repeat(10)]
        public async Task InterceptConsoleOutput_Synchronous_And_Asynchronous_Steps_Mix2_Called_Asynchronously()
        {
            var scenario = await When(() => Console.WriteLine("blah output when"))
                .Then(() => Console.Out.WriteLineAsync("blah output then"))
                .And(() => Console.WriteLine("blah output and then"))
                .BDTestAsync();

            Assert.That(scenario.Steps[0].Output, Is.EqualTo("blah output when"));
            Assert.That(scenario.Steps[1].Output, Is.EqualTo("blah output then"));
            Assert.That(scenario.Steps[2].Output, Is.EqualTo("blah output and then"));
        }
        
        // Sync - .BDTest()
                [Test]
        [Repeat(10)]
        public void InterceptConsoleOutput_Synchronous_Steps_Called_Synchronously()
        {
            var scenario = When(() => Console.WriteLine("blah output when"))
                .Then(() => Console.WriteLine("blah output then"))
                .And(() => Console.WriteLine("blah output and then"))
                .BDTest();

            Assert.That(scenario.Steps[0].Output, Is.EqualTo("blah output when"));
            Assert.That(scenario.Steps[1].Output, Is.EqualTo("blah output then"));
            Assert.That(scenario.Steps[2].Output, Is.EqualTo("blah output and then"));
        }
        
        [Test]
        [Repeat(10)]
        public void InterceptConsoleOutput_Asynchronous_Steps_Called_Synchronously()
        {
            var scenario = When(() => Console.Out.WriteLineAsync("blah output when"))
                .Then(() => Console.Out.WriteLineAsync("blah output then"))
                .And(() => Console.Out.WriteLineAsync("blah output and then"))
                .BDTest();
            
            Assert.That(scenario.Steps[0].Output, Is.EqualTo("blah output when"));
            Assert.That(scenario.Steps[1].Output, Is.EqualTo("blah output then"));
            Assert.That(scenario.Steps[2].Output, Is.EqualTo("blah output and then"));
        }
        
        [Test]
        [Repeat(10)]
        public void InterceptConsoleOutput_Synchronous_And_Asynchronous_Steps_Mix1_Called_Synchronously()
        {
            var scenario = When(() => Console.Out.WriteLineAsync("blah output when"))
                .Then(() => Console.WriteLine("blah output then"))
                .And(() => Console.Out.WriteLineAsync("blah output and then"))
                .BDTest();

            Assert.That(scenario.Steps[0].Output, Is.EqualTo("blah output when"));
            Assert.That(scenario.Steps[1].Output, Is.EqualTo("blah output then"));
            Assert.That(scenario.Steps[2].Output, Is.EqualTo("blah output and then"));
        }
        
        [Test]
        [Repeat(10)]
        public void InterceptConsoleOutput_Synchronous_And_Asynchronous_Steps_Mix2_Called_Synchronously()
        {
            var scenario = When(() => Console.WriteLine("blah output when"))
                .Then(() => Console.Out.WriteLineAsync("blah output then"))
                .And(() => Console.WriteLine("blah output and then"))
                .BDTest();

            Assert.That(scenario.Steps[0].Output, Is.EqualTo("blah output when"));
            Assert.That(scenario.Steps[1].Output, Is.EqualTo("blah output then"));
            Assert.That(scenario.Steps[2].Output, Is.EqualTo("blah output and then"));
        }
    }
}