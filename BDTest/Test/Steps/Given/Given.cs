using System;
using System.Linq.Expressions;

namespace BDTest.Test.Steps.Given
{
    public class Given : StepBuilder
    {

        protected override StepType StepType { get; } = StepType.Given;

        internal Given(Expression<Action> action) : base(action)
        {
        }

        public AndGiven And(Expression<Action> step)
        {
            return new AndGiven(ExistingSteps, step);
        }

        public When.When When(Expression<Action> step)
        {
            return new When.When(ExistingSteps, step);
        }
    }
}
