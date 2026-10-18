using System.Text.Json.Serialization;

namespace BDTest.Contracts;

/// <summary>The fluent step kind, retained separately from displayed step text.</summary>
[JsonConverter(typeof(StepTypeJsonConverter))]
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

internal sealed class StepTypeJsonConverter : JsonStringEnumConverter<StepType>
{
    public StepTypeJsonConverter() : base(namingPolicy: null, allowIntegerValues: false)
    {
    }
}
