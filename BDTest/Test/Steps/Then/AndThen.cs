using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace BDTest.Test.Steps.Then
{
    public class AndThen : Then
    {

        protected override StepType StepType { get; } = StepType.AndThen;

        // Actions
        protected internal AndThen(List<Step> previousSteps, Expression<Action> action, TestDetails testDetails) : base(previousSteps, action, testDetails)
        {
        }

        // Tasks
        protected internal AndThen(List<Step> previousSteps, Expression<Func<Task>> action, TestDetails testDetails) : base(previousSteps, action, testDetails)
        {
        }
        
        public AndThen WithStepText(Func<string> overridingStepText)
        {
            return WithStepText<AndThen>(overridingStepText);
        }
    }
}
