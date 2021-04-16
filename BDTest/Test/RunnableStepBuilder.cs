using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BDTest.Test.Steps;

namespace BDTest.Test
{
    public abstract class RunnableStepBuilder : StepBuilder
    {
        internal RunnableStepBuilder(List<Step> previousSteps, Runnable runnable, TestDetails testDetails) : base(
            previousSteps,
            runnable,
            testDetails)
        {
        }

        public RunnableStepBuilder WithScenarioText(string scenarioText)
        {
            var text = new ScenarioText(scenarioText);

            // TODO: Not need to set both
            ScenarioText = text;
            TestDetails.ScenarioText = text;
            
            return this;
        }

        internal async Task<Scenario> Invoke(TestDetails testDetails)
        {
            var scenario = new Scenario(ExistingSteps, testDetails);
            await BDTestServiceProvider.ScenarioExecutor.ExecuteAsync(scenario).ConfigureAwait(false);
            return scenario;
        }

        public Scenario BDTest()
        {
            return Invoke(TestDetails).GetAwaiter().GetResult();
        }

        public async Task<Scenario> BDTestAsync()
        {
            return await Invoke(TestDetails).ConfigureAwait(false);
        }

        public TaskAwaiter<Scenario> GetAwaiter()
            => Invoke(TestDetails).GetAwaiter();
    }
}