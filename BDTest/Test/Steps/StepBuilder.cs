using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using BDTest.Maps;

namespace BDTest.Test.Steps
{
    public abstract class StepBuilder : BuildableTest
    {

        internal readonly List<Step> ExistingSteps;
        protected abstract StepType StepType { get; }
        protected string StepPrefix => StepType.GetValue();

        protected StepBuilder(Expression<Action> action, string callerMember, string callerFile)
        {
            ExistingSteps = new List<Step> { new Step(action, StepType.Given) };
            TestDetails = new TestDetails(callerMember, callerFile, Guid.NewGuid());
        }

        

        internal StepBuilder(List<Step> previousSteps, Expression<Action> action, TestDetails testDetails)
        {
            TestDetails = testDetails;
            StoryText = testDetails.StoryText;
            ScenarioText = testDetails.ScenarioText;

            TestMap.NotRun[testDetails.GetGuid()] = this;

            ExistingSteps = previousSteps;
            ExistingSteps.Add(new Step(action, StepType));
        }

        protected Scenario Invoke(TestDetails testDetails)
        {
            return new Scenario(ExistingSteps, testDetails);
        }
    }
}
