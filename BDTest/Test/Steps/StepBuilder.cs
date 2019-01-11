using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace BDTest.Test.Steps
{
    public abstract class StepBuilder
    {

        internal readonly List<Step> ExistingSteps;
        protected abstract StepType StepType { get; }
        protected string StepPrefix => StepType.GetValue();

        protected StepBuilder(Expression<Action> action)
        {
            ExistingSteps = new List<Step> { new Step(action, StepType.Given) };
        }

        

        internal StepBuilder(List<Step> previousSteps, Expression<Action> action)
        {
            ExistingSteps = previousSteps;
            ExistingSteps.Add(new Step(action, StepType));
        }

        protected Scenario Invoke(string callerMember)
        {
            return new Scenario(ExistingSteps, callerMember);
        }
    }
}
