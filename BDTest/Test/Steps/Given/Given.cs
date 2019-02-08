using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using BDTest.Output;

namespace BDTest.Test.Steps.Given
{
    public class Given : StepBuilder
    {
        protected override StepType StepType { get; } = StepType.Given;

        // Actions
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

        // Tasks
        internal Given(Expression<Func<Task>> action, string callerMember, string callerFile) : base(action, callerMember, callerFile)
        {
        }

        public AndGiven And(Expression<Func<Task>> step)
        {
            return new AndGiven(ExistingSteps, step, TestDetails);
        }

        public When.When When(Expression<Func<Task>> step)
        {
            return new When.When(ExistingSteps, step, TestDetails);
        }
    }
}
