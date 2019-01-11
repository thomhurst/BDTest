using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace BDTest.Test.Steps.Then
{
    public class Then : StepBuilder
    {

        protected override StepType StepType { get; } = StepType.Then;

        protected internal Then(List<Step> previousSteps, Expression<Action> action) : base(previousSteps, action)
        {
        }

        public AndThen And(Expression<Action> step)
        {
            return new AndThen(ExistingSteps, step);
        }

        public Scenario BDTest([CallerMemberName] string callerMember = null)
        {
            return Invoke(callerMember);
        }
    }
}
