using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace BDTest.Test.Steps.When
{
    public class When : StepBuilder
    {
        protected override StepType StepType { get; } = StepType.When;

        internal When(List<Step> previousSteps, Runnable runnable, TestDetails testDetails) : base(previousSteps, runnable, testDetails)
        {
        }
        
        internal When(Runnable runnable, string callerMember, string callerFile, string testId, BDTestBase bdTestBase) : base(runnable, callerMember, callerFile, testId, StepType.When, bdTestBase)
        {
            // Used for skipping a 'Given' step
        }
        
        public When WithStepText(Func<string> overridingStepText)
        {
            return WithStepText<When>(overridingStepText);
        }
        
        // Actions
        public Then.Then Then(Expression<Action> step)
        {
            return new Then.Then(ExistingSteps, new Runnable(step), TestDetails);
        }

        // Tasks
        public Then.Then Then(Expression<Func<Task>> step)
        {
            return new Then.Then(ExistingSteps, new Runnable(step), TestDetails);
        }
    }
}
