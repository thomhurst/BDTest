using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace BDTest.Test.Steps.Given
{
    public class AndGiven : StepBuilder
    {

        protected override StepType StepType { get; } = StepType.AndGiven;

        internal AndGiven(List<Step> previousSteps, Runnable runnable, BuildableTest previousPartiallyBuiltTest) : base(previousSteps, runnable, previousPartiallyBuiltTest)
        {
        }

        public AndGiven WithStepText(Func<string> overridingStepText)
        {
            return WithStepText<AndGiven>(overridingStepText);
        }

        // Actions
        public AndGiven And(Expression<Action> step)
        {
            return new AndGiven(ExistingSteps, new Runnable(step), this);
        }

        public When.When When(Expression<Action> step)
        {
            return new When.When(ExistingSteps, new Runnable(step), this);
        }

        // Tasks
        public AndGiven And(Expression<Func<Task>> step)
        {
            return new AndGiven(ExistingSteps, new Runnable(step), this);
        }

        public When.When When(Expression<Func<Task>> step)
        {
            return new When.When(ExistingSteps, new Runnable(step), this);
        }
    }
}
