using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace BDTest.Test.Steps.When
{
    public class When : StepBuilder
    {

        protected override StepType StepType { get; } = StepType.When;

        // Actions
        protected internal When(List<Step> previousSteps, Expression<Action> action, TestDetails testDetails) : base(previousSteps, action, testDetails)
        {
        }
        
        internal When(Expression<Action> action, string callerMember, string callerFile, string testId) : base(action, callerMember, callerFile, testId)
        {
            // Used for skipping a 'Given' step
        }
        
        internal When(Expression<Func<Task>> action, string callerMember, string callerFile, string testId) : base(action, callerMember, callerFile, testId)
        {
            // Used for skipping a 'Given' step
        }
        
        public When WithStepText(Func<string> overridingStepText)
        {
            return WithStepText<When>(overridingStepText);
        }

        public Then.Then Then(Expression<Action> step)
        {
            return new Then.Then(ExistingSteps, step, TestDetails);
        }

        // Tasks
        protected internal When(List<Step> previousSteps, Expression<Func<Task>> action, TestDetails testDetails) : base(previousSteps, action, testDetails)
        {
        }

        public Then.Then Then(Expression<Func<Task>> step)
        {
            return new Then.Then(ExistingSteps, step, TestDetails);
        }
    }
}
