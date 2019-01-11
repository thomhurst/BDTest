using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace BDTest.Test.Steps.Then
{
    public class AndThen : Then
    {

        protected override StepType StepType { get; } = StepType.AndThen;

        protected internal AndThen(List<Step> previousSteps, Expression<Action> action) : base(previousSteps, action)
        {
        }
    }
}
