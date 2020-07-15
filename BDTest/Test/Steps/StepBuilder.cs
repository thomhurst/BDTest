using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BDTest.Maps;
using BDTest.Output;

namespace BDTest.Test.Steps
{
    public abstract class StepBuilder : BuildableTest
    {

        internal readonly List<Step> ExistingSteps;
        protected abstract StepType StepType { get; }
        protected string StepPrefix => StepType.GetValue();

        static StepBuilder()
        {
            InternalTestTimeData.TestsStartedAt = DateTime.Now;
            Initialiser.Initialise();
        }

        protected T WithStepText<T>(Func<string> overridingStepText) where T : StepBuilder
        {
            ExistingSteps.Last().OverriddenStepText = overridingStepText;
            return (T) this;
        }

        // First Test Construction - Given or When
        internal StepBuilder(Runnable runnable, string callerMember, string callerFile, string testId, StepType stepType)
        {
            ExistingSteps = new List<Step> { new Step(runnable, stepType) };
            
            var testGuid = System.Guid.NewGuid();
            TestDetails = new TestDetails(callerMember, callerFile, testGuid, testId);
            
            TestOutputData.TestId = testGuid;
            
            TestHolder.NotRun[testGuid.ToString()] = this;
            
            StoryText = TestDetails.StoryText;
            ScenarioText = TestDetails.ScenarioText;
        }

        internal StepBuilder(List<Step> previousSteps, Runnable runnable, TestDetails testDetails)
        {
            TestDetails = testDetails;
            StoryText = testDetails.StoryText;
            ScenarioText = testDetails.ScenarioText;

            TestHolder.NotRun[testDetails.GetGuid()] = this;

            ExistingSteps = previousSteps;
            ExistingSteps.Add(new Step(runnable, StepType));
        }

        internal async Task<Scenario> Invoke(TestDetails testDetails)
        {
            var scenario = new Scenario(ExistingSteps, testDetails);
            await scenario.Execute();
            return scenario;
        }
    }
}
