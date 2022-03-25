using System;
using System.Collections.Generic;

namespace BDTest.Test.Steps.Then;

public class AndThen : Then
{
    protected override StepType StepType { get; } = StepType.AndThen;

    // Actions
    internal AndThen(List<Step> previousSteps, Runnable runnable, BuildableTest previousPartiallyBuiltTest) : base(previousSteps, runnable, previousPartiallyBuiltTest)
    {
    }
        
    public new AndThen WithStepText(Func<string> overridingStepText)
    {
        return WithStepText<AndThen>(overridingStepText);
    }
}