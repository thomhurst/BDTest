using BDTest;

namespace BDTest.Test;

// Only configuration and lazy delegates live here; execution allocates a Step.
internal sealed class StepDefinition
{
    internal Runnable Runnable { get; }
    internal StepType StepType { get; }
    internal TimeSpan? Timeout { get; set; }
    internal Func<string?>? OverriddenStepText { get; set; }
    private IReadOnlyList<Func<object?>>? _explicitArguments;
    internal IReadOnlyList<Func<object?>>? ExplicitArguments
    {
        get => _explicitArguments;
        set
        {
            var template = Runnable.Definition.GetText(explicitArguments: value);
            _explicitArguments = value;
            TextTemplate = template;
        }
    }
    internal List<Func<StepArgument>> AttachedArguments { get; private set; } = new();
    internal string TextTemplate { get; private set; }

    internal StepDefinition(Runnable runnable, StepType stepType)
    {
        Runnable = runnable;
        StepType = stepType;
        TextTemplate = runnable.Definition.GetText();
    }

    internal StepDefinition Snapshot()
    {
        var copy = (StepDefinition)MemberwiseClone();
        copy._explicitArguments = _explicitArguments?.ToArray();
        copy.AttachedArguments = new List<Func<StepArgument>>(AttachedArguments);
        return copy;
    }
}
