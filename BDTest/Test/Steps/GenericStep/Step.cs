using System.Linq.Expressions;

namespace BDTest.Test.Steps.GenericStep;

public class GenericStep : RunnableStepBuilder
{
    protected override StepType StepType { get; } = StepType.Step;

    internal GenericStep(Runnable runnable, string callerMember, string callerFile, string testId, string reportId, BDTestBase bdTestBase) 
        : base(new List<Test.Step>(), runnable, new TestStepBuilder(runnable, callerMember, callerFile, testId, reportId, StepType.Step, bdTestBase))
    {
    }

    internal GenericStep(List<Test.Step> previousSteps, Runnable runnable, BuildableTest previousPartiallyBuiltTest) 
        : base(previousSteps, runnable, previousPartiallyBuiltTest)
    {
    }

    public GenericStep WithStepText(Func<string> overridingStepText)
    {
        return WithStepText<GenericStep>(overridingStepText);
    }

    // Actions
    public GenericStep Step(Expression<Action> step)
    {
        return new GenericStep(ExistingSteps, new Runnable(step), this);
    }

    // Tasks
    public GenericStep Step(Expression<Func<Task>> step)
    {
        return new GenericStep(ExistingSteps, new Runnable(step), this);
    }

    // Internal helper class to properly initialize the first step
    private class TestStepBuilder : StepBuilder
    {
        protected override StepType StepType { get; } = StepType.Step;

        internal TestStepBuilder(Runnable runnable, string callerMember, string callerFile, string testId, string reportId, StepType stepType, BDTestBase bdTestBase)
            : base(runnable, callerMember, callerFile, testId, reportId, stepType, bdTestBase)
        {
        }
    }
}