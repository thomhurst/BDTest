namespace BDTest.Test.Steps;

public enum StepType
{
    Given,
    AndGiven,
    When,
    Then,
    AndThen
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
            default:
                throw new ArgumentOutOfRangeException(nameof(stepType), stepType, null);
        }
    }
}