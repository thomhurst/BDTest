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
    ButThen,
    AndWhen
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
            case StepType.AndWhen:
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