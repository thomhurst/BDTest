using System;
using System.Collections.Generic;
using System.Linq.Expressions;
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
            Initialiser.Initialise();
        }

        protected StepBuilder(Expression<Action> action, string callerMember, string callerFile, string testId, StepType stepType) : this(new Runnable(action), callerMember, callerFile, testId, stepType)
        {
            
        }

        protected StepBuilder(Expression<Func<Task>> action, string callerMember, string callerFile, string testId, StepType stepType) : this(new Runnable(action), callerMember, callerFile, testId, stepType)
        {
            
        }

        protected T WithStepText<T>(Func<string> overridingStepText) where T : StepBuilder
        {
            ExistingSteps[TestDetails.StepCount - 1].OverriddenStepText = overridingStepText;
            return (T) this;
        }

        private StepBuilder(Runnable runnable, string callerMember, string callerFile, string testId, StepType stepType)
        {
            ExistingSteps = new List<Step> { new Step(runnable, stepType) };
            
            var testGuid = System.Guid.NewGuid();
            TestDetails = new TestDetails(callerMember, callerFile, testGuid, testId);
            
            TestOutputData.TestId = testGuid;
            
            TestHolder.NotRun[testGuid.ToString()] = this;
            
            StoryText = TestDetails.StoryText;
            ScenarioText = TestDetails.ScenarioText;
        }

        protected StepBuilder(List<Step> previousSteps, Expression<Action> action, TestDetails testDetails) : this(previousSteps, new Runnable(action), testDetails )
        {
        }

        protected StepBuilder(List<Step> previousSteps, Expression<Func<Task>> action, TestDetails testDetails) : this(previousSteps, new Runnable(action), testDetails)
        {
        }

        private StepBuilder(List<Step> previousSteps, Runnable runnable, TestDetails testDetails)
        {
            testDetails.StepCount++;
            
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
