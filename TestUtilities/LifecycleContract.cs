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

    public static async Task VerifyLegacy(Func<BDTestRun, Probe> create)
    {
        using var run = BDTestRun.Start();
        var probe = create(run);
        probe.Begin("ignored-runner-id");
        var originalContext = probe.Context();
        Console.WriteLine("legacy startup");
        var scenario = await probe.Fixture.When(() => NoOp()).Then(() => NoOp()).BDTestAsync();
        Require(scenario.FrameworkTestId == "legacy-id", "Scenario ignored the legacy ID override.");
        Require(scenario.TestStartupInformation?.Contains("legacy startup") == true, "Startup capture used a different ID.");
        probe.End();
        probe.End();
        probe.Begin("another-runner-id");
        Require(!ReferenceEquals(originalContext, probe.Context()), "Completion retained the overridden context.");
        probe.Fixture.WriteStartupOutput("pending legacy output");
        probe.Fixture.ScenarioHtmlWriter.Text("pending legacy HTML");
        probe.End();
        probe.Begin("third-runner-id");
        var next = await probe.Fixture.When(() => NoOp()).Then(() => NoOp()).BDTestAsync();
        Require(string.IsNullOrEmpty(next.TestStartupInformation), "Completion retained pending legacy output.");
        Require(string.IsNullOrEmpty(next.CustomHtmlOutputForReport), "Completion retained pending legacy HTML.");
        probe.End();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void NoOp() { }
}
