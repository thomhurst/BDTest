using System.Runtime.CompilerServices;
using System.Threading;
using BDTest.Exceptions;
using BDTest.Test.Steps;
using BDTest.Output;

namespace BDTest.Test;

/// <summary>A scenario builder that supports one execution through BDTestAsync() or direct await.</summary>
/// <remarks>Repeated or concurrent execution attempts throw <see cref="AlreadyExecutedException"/>, even after failure.
/// Each continuation is an independent builder. Internal retries remain part of the original execution.</remarks>
public abstract class RunnableStepBuilder : StepBuilder
{
    private int _executionStarted;

    internal RunnableStepBuilder(Runnable runnable, BuildableTest previousPartiallyBuiltTest) : base(
        runnable,
        previousPartiallyBuiltTest)
    {
    }

    protected void SetScenarioText(string scenarioText)
    {
        ScenarioText = new ScenarioText(scenarioText);
    }

    internal Task<Scenario> Invoke(BuildableTest testDetails,
        Scenario? outline = null, Dictionary<string, string>? exampleValues = null)
    {
        testDetails.Run.ThrowIfDisposed();
        if (Interlocked.Exchange(ref _executionStarted, 1) != 0)
        {
            throw new AlreadyExecutedException("This scenario builder has already started execution. Create a new builder to run another scenario.");
        }

        // Select the executing branch, which need not be the last one built.
        // Set these before entering an async method so teardown keeps this ID too.
        TestOutputData.Run = testDetails.Run;
        TestOutputData.TestId = testDetails.Guid;
        TestOutputData.FrameworkExecutionId = testDetails.TestId;
        // Keep reusable builder definitions separate from report steps, whose
        // runtime references are released after the final execution attempt.
        var definition = testDetails.Definition.Snapshot();
        var steps = definition.Steps.Select(step => new Step(step, definition.Guid, definition.ReportId, definition.Run)).ToList();
        var scenario = new Scenario(steps, definition, outline, exampleValues);
        foreach (var step in steps)
        {
            step.ExampleValues = exampleValues;
        }
        return ExecuteAsync(scenario);
    }

    private static async Task<Scenario> ExecuteAsync(Scenario scenario)
    {
        await scenario.ExecutionRun.ScenarioExecutor.ExecuteAsync(scenario);
        return scenario;
    }

    /// <exception cref="AlreadyExecutedException">Execution has already started on this builder.</exception>
    public Task<Scenario> BDTestAsync()
    {
        return Invoke(this);
    }

    /// <exception cref="AlreadyExecutedException">Execution has already started on this builder.</exception>
    public TaskAwaiter<Scenario> GetAwaiter() => Invoke(this).GetAwaiter();
}
