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

        protected StepBuilder(Expression<Action> action, string callerMember, string callerFile) : this(new Runnable(action), callerMember, callerFile )
        {
            
        }

        protected StepBuilder(Expression<Func<Task>> action, string callerMember, string callerFile) : this(new Runnable(action), callerMember, callerFile)
        {
            
        }

        internal StepBuilder(Runnable runnable, string callerMember, string callerFile)
        {
            ExistingSteps = new List<Step> { new Step(runnable, StepType.Given) };
            TestDetails = new TestDetails(callerMember, callerFile, Guid.NewGuid());
        }

        protected StepBuilder(List<Step> previousSteps, Expression<Action> action, TestDetails testDetails) : this(previousSteps, new Runnable(action), testDetails )
        {
        }

        protected StepBuilder(List<Step> previousSteps, Expression<Func<Task>> action, TestDetails testDetails) : this(previousSteps, new Runnable(action), testDetails)
        {
        }

        internal StepBuilder(List<Step> previousSteps, Runnable runnable, TestDetails testDetails)
        {
            TestDetails = testDetails;
            StoryText = testDetails.StoryText;
            ScenarioText = testDetails.ScenarioText;

            TestMap.NotRun[testDetails.GetGuid()] = this;

            ExistingSteps = previousSteps;
            ExistingSteps.Add(new Step(runnable, StepType));
        }

        protected async Task<Scenario> Invoke(TestDetails testDetails)
        {
            var scenario = new Scenario(ExistingSteps, testDetails);
            await scenario.Execute();
            return scenario;
        }
    }
}
