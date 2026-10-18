using System.Text.Json.Serialization;

namespace BDTest.Test;

/// <summary>Serializable provenance of an intentional step skip. No attributes or delegates are retained.</summary>
public sealed class StepSkipReason
{
    /// <summary>Rule or DebugSetting.</summary>
    public string Source { get; }
    /// <summary>Full name of the matching attribute type, for a rule.</summary>
    public string? AttributeType { get; }
    /// <summary>Zero-based index in the registration snapshot evaluated for this attempt.</summary>
    public int? RuleIndex { get; }
    /// <summary>Name of the debug setting, for a debug skip.</summary>
    public string? Setting { get; }

    [JsonConstructor]
    public StepSkipReason(string source, string? attributeType = null, int? ruleIndex = null, string? setting = null)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        AttributeType = attributeType;
        RuleIndex = ruleIndex;
        Setting = setting;
    }

    public override string ToString() => Source == "Rule"
        ? $"Skip rule {RuleIndex} matched {AttributeType}."
        : Source == "DebugSetting" ? $"Skipped by {Setting}." : Source;
}
