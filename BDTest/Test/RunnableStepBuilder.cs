using System.Runtime.CompilerServices;
using System.Threading;
using BDTest.Exceptions;
using BDTest.Test.Steps;
using BDTest.Output;

namespace BDTest.Test;

/// <summary>A scenario builder that supports one execution through BDTest(), BDTestAsync(), or direct await.</summary>
/// <remarks>Repeated or concurrent execution attempts throw <see cref="AlreadyExecutedException"/>, even after failure.
/// Each continuation is an independent builder. Internal retries remain part of the original execution.</remarks>
public abstract class RunnableStepBuilder : StepBuilder
{
    private int _executionStarted;

    internal RunnableStepBuilder(List<Step> previousSteps, Runnable runnable, BuildableTest previousPartiallyBuiltTest) : base(
        previousSteps,
        runnable,
        previousPartiallyBuiltTest)
    {
    }

    public RunnableStepBuilder WithScenarioText(string scenarioText)
    {
        var text = new ScenarioText(scenarioText);
            
        ScenarioText = text;

        return this;
    }

    internal Task<Scenario> Invoke(BuildableTest testDetails, bool runOnThreadPool = false)
    {
        if (Interlocked.Exchange(ref _executionStarted, 1) != 0)
        {
            throw new AlreadyExecutedException("This scenario builder has already started execution. Create a new builder to run another scenario.");
        }

        // Select the executing branch, which need not be the last one built.
        // Set these before entering an async method so teardown keeps this ID too.
        TestOutputData.TestId = testDetails.Guid;
        TestOutputData.FrameworkExecutionId = testDetails.TestId;
        // Keep reusable builder definitions separate from report steps, whose
        // runtime references are released after the final execution attempt.
        var steps = ExistingSteps.Select(step => new Step(step, testDetails.Guid)).ToList();
        var scenario = new Scenario(steps, testDetails);
        // Only the blocking entry point needs a scheduling boundary. Keep IDs in
        // the caller's flow so output written after BDTest() still reaches teardown.
        return runOnThreadPool ? Task.Run(() => ExecuteAsync(scenario)) : ExecuteAsync(scenario);
    }

    private static async Task<Scenario> ExecuteAsync(Scenario scenario)
    {
        await BDTestServiceProvider.ScenarioExecutor.ExecuteAsync(scenario).ConfigureAwait(false);
        return scenario;
    }

    /// <exception cref="AlreadyExecutedException">Execution has already started on this builder.</exception>
    /// <remarks>Runs the scenario on the thread pool and blocks until completion. Prefer BDTestAsync() for async callers.</remarks>
    public Scenario BDTest()
    {
        return Invoke(this, runOnThreadPool: true).GetAwaiter().GetResult();
    }

    /// <exception cref="AlreadyExecutedException">Execution has already started on this builder.</exception>
    public Task<Scenario> BDTestAsync()
    {
        return Invoke(this);
    }

    /// <exception cref="AlreadyExecutedException">Execution has already started on this builder.</exception>
    public TaskAwaiter<Scenario> GetAwaiter() => Invoke(this).GetAwaiter();
}
