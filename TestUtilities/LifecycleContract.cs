using BDTest.Test;

namespace BDTest.TestUtilities;

// Shared public-API contract, exercised through each framework adapter.
public static class LifecycleContract
{
    public sealed record Probe(BDTestBase Fixture, Action<string> Begin, Action End, Func<object> Context);

    public static async Task Verify(Func<BDTestRun, Probe> create)
    {
        using var firstRun = BDTestRun.Start();
        using var secondRun = BDTestRun.Start();
        var first = create(firstRun);
        var second = create(secondRun);
        first.Begin("shared-id");
        var originalContext = first.Context();
        Console.WriteLine("first startup");
        first.Fixture.ScenarioHtmlWriter.Text("first pending html");
        second.Begin("shared-id");
        Console.WriteLine("second startup");
        first.End();
        first.End();
        Console.WriteLine("second after first completion");
        var scenario = await second.Fixture.When(() => NoOp()).Then(() => NoOp()).BDTestAsync();
        Require(scenario.TestStartupInformation.Contains("second startup") &&
                scenario.TestStartupInformation.Contains("second after first completion") &&
                !scenario.TestStartupInformation.Contains("first startup"), "Run startup output leaked or was lost.");
        second.End();
        second.End();
        Console.WriteLine("output outside any test");
        var teardown = scenario.TearDownOutput;
        first.Begin("shared-id");
        Require(!ReferenceEquals(originalContext, first.Context()), "Completion retained the old context.");
        var next = await first.Fixture.When(() => NoOp()).Then(() => NoOp()).BDTestAsync();
        Require(string.IsNullOrEmpty(next.TestStartupInformation), "Completion retained pending startup output.");
        Require(string.IsNullOrEmpty(next.CustomHtmlOutputForReport), "Completion retained pending HTML listeners.");
        Require(scenario.TearDownOutput == teardown, "Output after completion reached the previous run.");
        Require(firstRun.Scenarios.Count == 1 && secondRun.Scenarios.Count == 1, "Run scenario stores leaked.");
        first.End();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void NoOp() { }
}
