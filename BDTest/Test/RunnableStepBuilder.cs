using System.Runtime.CompilerServices;
using BDTest.Test.Steps;

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

    internal async Task<Scenario> Invoke(BuildableTest testDetails)
    {
        var scenario = new Scenario(ExistingSteps, testDetails);
        await BDTestServiceProvider.ScenarioExecutor.ExecuteAsync(scenario).ConfigureAwait(false);
        return scenario;
    }

    public Scenario BDTest()
    {
        return Invoke(this).GetAwaiter().GetResult();
    }

    public async Task<Scenario> BDTestAsync()
    {
        return await this;
    }

    public TaskAwaiter<Scenario> GetAwaiter() => Invoke(this).GetAwaiter();
}