using System.Text.Json.Serialization;

namespace BDTest.Contracts;

/// <summary>Unevaluated step text and settings for a pending definition.</summary>
public sealed class StepDefinitionSummary
{
    public StepType StepType { get; }
    public string StepText { get; }
    public TimeSpan? Timeout { get; }

    [JsonConstructor]
    public StepDefinitionSummary(StepType stepType, string stepText, TimeSpan? timeout = null)
    {
        StepType = Enum.IsDefined(typeof(StepType), stepType)
            ? stepType : throw new ArgumentOutOfRangeException(nameof(stepType));
        StepText = stepText ?? throw new ArgumentNullException(nameof(stepText));
        Timeout = timeout;
    }
}
