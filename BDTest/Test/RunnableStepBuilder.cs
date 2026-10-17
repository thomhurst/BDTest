using System.Runtime.CompilerServices;
using BDTest.Test.Steps;
using BDTest.Output;

namespace BDTest.Test;

public abstract class RunnableStepBuilder : StepBuilder
{
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

    internal Task<Scenario> Invoke(BuildableTest testDetails)
    {
        // Select the executing branch, which need not be the last one built.
        // Set these before entering an async method so teardown keeps this ID too.
        TestOutputData.TestId = testDetails.Guid;
        TestOutputData.FrameworkExecutionId = testDetails.TestId;
        return ExecuteAsync(new Scenario(ExistingSteps, testDetails));
    }

    private static async Task<Scenario> ExecuteAsync(Scenario scenario)
    {
        await BDTestServiceProvider.ScenarioExecutor.ExecuteAsync(scenario).ConfigureAwait(false);
        return scenario;
    }

    public Scenario BDTest()
    {
        return Invoke(this).GetAwaiter().GetResult();
    }

    public Task<Scenario> BDTestAsync()
    {
        return Invoke(this);
    }

    public TaskAwaiter<Scenario> GetAwaiter() => Invoke(this).GetAwaiter();
}