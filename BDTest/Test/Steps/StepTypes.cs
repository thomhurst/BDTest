using BDTest.Contracts;
namespace BDTest;

public static class StepTypeExtensions
{
    public static string GetValue(this StepType stepType)
    {
        switch (stepType)
        {
            case StepType.Given:
            case StepType.When:
            case StepType.Then:
            case StepType.Step:
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