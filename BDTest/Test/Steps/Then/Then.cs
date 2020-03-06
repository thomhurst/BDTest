using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace BDTest.Test.Steps.Then
{
    public class Then : StepBuilder
    {

        protected override StepType StepType { get; } = StepType.Then;

        // Actions
        protected internal Then(List<Step> previousSteps, Expression<Action> action, TestDetails testDetails) : base(previousSteps, action, testDetails)
        {
        }

        public AndThen And(Expression<Action> step)
        {
            return new AndThen(ExistingSteps, step, TestDetails);
        }

        // Tasks
        protected internal Then(List<Step> previousSteps, Expression<Func<Task>> action, TestDetails testDetails) : base(previousSteps, action, testDetails)
        {
        }
        
        public Then WithStepText(Func<string> overridingStepText)
        {
            return WithStepText<Then>(overridingStepText);
        }

        public AndThen And(Expression<Func<Task>> step)
        {
            return new AndThen(ExistingSteps, step, TestDetails);
        }

        public Scenario BDTest()
        {
            return Invoke(TestDetails).GetAwaiter().GetResult();
        }

        public async Task<Scenario> BDTestAsync()
        {
            return await Invoke(TestDetails);
        }
    }
}
