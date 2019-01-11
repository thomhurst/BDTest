using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace BDTest.Test.Steps.When
{
    public class When : StepBuilder
    {

        protected override StepType StepType { get; } = StepType.When;

        protected internal When(List<Step> previousSteps, Expression<Action> action) : base(previousSteps, action)
        {
        }

        public Then.Then Then(Expression<Action> step)
        {
            return new Then.Then(ExistingSteps, step);
        }
    }
}
