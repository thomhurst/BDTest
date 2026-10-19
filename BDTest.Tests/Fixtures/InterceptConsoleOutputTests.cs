using BDTest.Attributes;
using BDTest.Test;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[NotInParallel]
[Story(AsA = "BDTest developer",
    IWant = "to make sure that I can intercept console output",
    SoThat = "tests contain useful information")]
public class InterceptConsoleOutputTests : RunTestBase
{
    // Async - .BDTestAsync()
    [Test]
    [Repeat(9)]
    public async Task InterceptConsoleOutput_Synchronous_Steps_Called_Asynchronously()
    {
        var scenario = await When(() => Console.WriteLine("blah output when"))
            .Then(() => Console.WriteLine("blah output then"))
            .And(() => Console.WriteLine("blah output and then"))
            .BDTestAsync();

        await Assert.That(Required(scenario.Steps)[0].Output).IsEqualTo("blah output when");
        await Assert.That(scenario.Steps[1].Output).IsEqualTo("blah output then");
        await Assert.That(scenario.Steps[2].Output).IsEqualTo("blah output and then");
    }
        
    [Test]
    [Repeat(9)]
    public async Task InterceptConsoleOutput_Asynchronous_Steps_Called_Asynchronously()
    {
        var scenario = await When(() => Console.Out.WriteLineAsync("blah output when"))
            .Then(() => Console.Out.WriteLineAsync("blah output then"))
            .And(() => Console.Out.WriteLineAsync("blah output and then"))
            .BDTestAsync();
            
        await Assert.That(Required(scenario.Steps)[0].Output).IsEqualTo("blah output when");
        await Assert.That(scenario.Steps[1].Output).IsEqualTo("blah output then");
        await Assert.That(scenario.Steps[2].Output).IsEqualTo("blah output and then");
    }
        
    [Test]
    [Repeat(9)]
    public async Task InterceptConsoleOutput_Synchronous_And_Asynchronous_Steps_Mix1_Called_Asynchronously()
    {
        var scenario = await When(() => Console.Out.WriteLineAsync("blah output when"))
            .Then(() => Console.WriteLine("blah output then"))
            .And(() => Console.Out.WriteLineAsync("blah output and then"))
            .BDTestAsync();

        await Assert.That(Required(scenario.Steps)[0].Output).IsEqualTo("blah output when");
        await Assert.That(scenario.Steps[1].Output).IsEqualTo("blah output then");
        await Assert.That(scenario.Steps[2].Output).IsEqualTo("blah output and then");
    }
        
    [Test]
    [Repeat(9)]
    public async Task InterceptConsoleOutput_Synchronous_And_Asynchronous_Steps_Mix2_Called_Asynchronously()
    {
        var scenario = await When(() => Console.WriteLine("blah output when"))
            .Then(() => Console.Out.WriteLineAsync("blah output then"))
            .And(() => Console.WriteLine("blah output and then"))
            .BDTestAsync();

        await Assert.That(Required(scenario.Steps)[0].Output).IsEqualTo("blah output when");
        await Assert.That(scenario.Steps[1].Output).IsEqualTo("blah output then");
        await Assert.That(scenario.Steps[2].Output).IsEqualTo("blah output and then");
    }
        
    // Direct await
    [Test]
    [Repeat(9)]
    public async Task InterceptConsoleOutput_Synchronous_Steps_Called_With_Direct_Await()
    {
        var scenario = await When(() => Console.WriteLine("blah output when"))
            .Then(() => Console.WriteLine("blah output then"))
            .And(() => Console.WriteLine("blah output and then"));

        await Assert.That(Required(scenario.Steps)[0].Output).IsEqualTo("blah output when");
        await Assert.That(scenario.Steps[1].Output).IsEqualTo("blah output then");
        await Assert.That(scenario.Steps[2].Output).IsEqualTo("blah output and then");
    }
        
    [Test]
    [Repeat(9)]
    public async Task InterceptConsoleOutput_Asynchronous_Steps_Called_With_Direct_Await()
    {
        var scenario = await When(() => Console.Out.WriteLineAsync("blah output when"))
            .Then(() => Console.Out.WriteLineAsync("blah output then"))
            .And(() => Console.Out.WriteLineAsync("blah output and then"));
            
        await Assert.That(Required(scenario.Steps)[0].Output).IsEqualTo("blah output when");
        await Assert.That(scenario.Steps[1].Output).IsEqualTo("blah output then");
        await Assert.That(scenario.Steps[2].Output).IsEqualTo("blah output and then");
    }
        
    [Test]
    [Repeat(9)]
    public async Task InterceptConsoleOutput_Synchronous_And_Asynchronous_Steps_Mix1_Called_With_Direct_Await()
    {
        var scenario = await When(() => Console.Out.WriteLineAsync("blah output when"))
            .Then(() => Console.WriteLine("blah output then"))
            .And(() => Console.Out.WriteLineAsync("blah output and then"));

        await Assert.That(Required(scenario.Steps)[0].Output).IsEqualTo("blah output when");
        await Assert.That(scenario.Steps[1].Output).IsEqualTo("blah output then");
        await Assert.That(scenario.Steps[2].Output).IsEqualTo("blah output and then");
    }
        
    [Test]
    [Repeat(9)]
    public async Task InterceptConsoleOutput_Synchronous_And_Asynchronous_Steps_Mix2_Called_With_Direct_Await()
    {
        var scenario = await When(() => Console.WriteLine("blah output when"))
            .Then(() => Console.Out.WriteLineAsync("blah output then"))
            .And(() => Console.WriteLine("blah output and then"));

        await Assert.That(Required(scenario.Steps)[0].Output).IsEqualTo("blah output when");
        await Assert.That(scenario.Steps[1].Output).IsEqualTo("blah output then");
        await Assert.That(scenario.Steps[2].Output).IsEqualTo("blah output and then");
    }
}
