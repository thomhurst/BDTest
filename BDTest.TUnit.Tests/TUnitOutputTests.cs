namespace BDTest.TUnit.Tests;

public class TUnitOutputTests : TUnitBDTestBase<TUnitContext>
{
    [Before(HookType.Test)]
    public void WriteFromBeforeHook()
    {
        Console.WriteLine($"hook:{TestContext.Current!.Id}");
    }

    [Test]
    [Repeat(3)]
    public async Task ConsoleOutputIsCapturedFromHookBeforeGivenAndDuringSteps()
    {
        var id = TestContext.Current!.Id;
        Console.WriteLine($"before:{id}");
        await Task.Yield();

        var scenario = await Given(() => Console.WriteLine($"given:{id}"))
            .When(() => WriteAfterYield(id))
            .Then(() => Console.WriteLine($"then:{id}"))
            .BDTestAsync();

        await Assert.That(scenario.FrameworkTestId).IsEqualTo(id);
        await Assert.That(scenario.TestStartupInformation)
            .IsEqualTo($"hook:{id}{Environment.NewLine}before:{id}{Environment.NewLine}");
        var steps = scenario.Steps ?? throw new InvalidOperationException("Expected recorded scenario steps.");
        await Assert.That(steps[0].Output).IsEqualTo($"given:{id}");
        await Assert.That(steps[1].Output).IsEqualTo($"when:{id}");
        await Assert.That(steps[2].Output).IsEqualTo($"then:{id}");
    }

    [Test]
    public async Task WriteStartupOutputIsRecordedAgainstTUnitTest()
    {
        var id = TestContext.Current!.Id;
        WriteStartupOutput($"explicit:{id}");
        await Task.Yield();

        var scenario = await Given(() => Console.WriteLine("step output"))
            .When(() => Console.WriteLine("when output"))
            .Then(() => Console.WriteLine("then output"))
            .BDTestAsync();

        await Assert.That(scenario.FrameworkTestId).IsEqualTo(id);
        await Assert.That(scenario.TestStartupInformation)
            .IsEqualTo($"hook:{id}{Environment.NewLine}explicit:{id}{Environment.NewLine}");
        var steps = scenario.Steps ?? throw new InvalidOperationException("Expected recorded scenario steps.");
        await Assert.That(steps[0].Output).IsEqualTo("step output");
    }

    private static async Task WriteAfterYield(string id)
    {
        await Task.Yield();
        Console.WriteLine($"when:{id}");
    }
}
