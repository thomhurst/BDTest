namespace BDTest.Test.Steps;

public enum StepType
{
    Given,
    AndGiven,
    When,
    Then,
    AndThen,
    ButGiven,
    ButWhen,
    ButThen
}

public static class StepTypeExtensions
{
    public static string GetValue(this StepType stepType)
    {
        switch (stepType)
        {
            case StepType.Given:
            case StepType.When:
            case StepType.Then:
                return stepType.ToString();
            case StepType.AndGiven:
            case StepType.AndThen:
                return "And";
            case StepType.ButGiven:
            case StepType.ButWhen:
            case StepType.ButThen:
                return "But";
            default:
                throw new ArgumentOutOfRangeException(nameof(stepType), stepType, null);
        }
    }
}