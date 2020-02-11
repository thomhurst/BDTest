using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace BDTest.Test.Steps.Given
{
    public class AndGiven : StepBuilder
    {

        protected override StepType StepType { get; } = StepType.AndGiven;

        public AndGiven WithStepText(Func<string> overridingStepText)
        {
            return WithStepText<AndGiven>(overridingStepText);
        }

        // Actions
        public AndGiven And(Expression<Action> step)
        {
            return new AndGiven(ExistingSteps, step, TestDetails);
        }

        public When.When When(Expression<Action> step)
        {
            return new When.When(ExistingSteps, step, TestDetails);
        }

        public AndGiven(List<Step> previousSteps, Expression<Action> action, TestDetails testDetails) : base(previousSteps, action, testDetails)
        {
        }

        // Tasks
        public AndGiven And(Expression<Func<Task>> step)
        {
            return new AndGiven(ExistingSteps, step, TestDetails);
        }

        public When.When When(Expression<Func<Task>> step)
        {
            return new When.When(ExistingSteps, step, TestDetails);
        }

        public AndGiven(List<Step> previousSteps, Expression<Func<Task>> action, TestDetails testDetails) : base(previousSteps, action, testDetails)
        {
        }
    }
}
