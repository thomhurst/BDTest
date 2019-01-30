using System;
using System.Linq.Expressions;
using BDTest.Output;

namespace BDTest.Test.Steps.Given
{
    public class Given : StepBuilder
    {

        static Given()
        {
            WriteOutput.Initialise();
        }

        protected override StepType StepType { get; } = StepType.Given;

        internal Given(Expression<Action> action, string callerMember, string callerFile) : base(action, callerMember, callerFile)
        {
        }

        public AndGiven And(Expression<Action> step)
        {
            return new AndGiven(ExistingSteps, step, TestDetails);
        }

        public When.When When(Expression<Action> step)
        {
            return new When.When(ExistingSteps, step, TestDetails);
        }
    }
}
