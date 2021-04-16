using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace BDTest.Test.Steps.Then
{
    public class Then : RunnableStepBuilder
    {
        protected override StepType StepType { get; } = StepType.Then;

        internal Then(List<Step> previousSteps, Runnable runnable, TestDetails testDetails) : base(previousSteps, runnable, testDetails)
        {
        }

        public Then WithStepText(Func<string> overridingStepText)
        {
            return WithStepText<Then>(overridingStepText);
        }

        // Actions
        public AndThen And(Expression<Action> step)
        {
            return new AndThen(ExistingSteps, new Runnable(step), TestDetails);
        }

        public AndThen And(Expression<Func<Task>> step)
        {
            return new AndThen(ExistingSteps, new Runnable(step), TestDetails);
        }
    }
}
