using System;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace BDTest.Test.Steps.Given
{
    public class Given : StepBuilder
    {
        protected override StepType StepType { get; } = StepType.Given;
        
        internal Given(Runnable runnable, string callerMember, string callerFile, string testId, string reportId, BDTestBase bdTestBase) : base(runnable, callerMember, callerFile, testId, reportId, StepType.Given, bdTestBase)
        {
        }
        
        public Given WithStepText(Func<string> overridingStepText)
        {
            return WithStepText<Given>(overridingStepText);
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
