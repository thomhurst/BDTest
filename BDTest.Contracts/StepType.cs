namespace BDTest.Contracts;

/// <summary>The fluent step kind, retained separately from displayed step text.</summary>
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
