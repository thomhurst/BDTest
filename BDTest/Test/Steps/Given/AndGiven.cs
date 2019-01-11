using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace BDTest.Test.Steps.Given
{
    public class AndGiven : StepBuilder
    {

        protected override StepType StepType { get; } = StepType.AndGiven;

        

        public AndGiven And(Expression<Action> step)
        {
            return new AndGiven(ExistingSteps, step);
        }

        public When.When When(Expression<Action> step)
        {
            return new When.When(ExistingSteps, step);
        }

        public AndGiven(List<Step> previousSteps, Expression<Action> action) : base(previousSteps, action)
        {
        }
    }
}
