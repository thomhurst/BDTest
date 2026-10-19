namespace BDTest;

/// <summary>Customizes a step's report text using parameter names, for example <c>{amount:F2}</c>.</summary>
/// <remarks>Display overrides use <c>parameter=replacement</c>. Invalid templates are diagnosed when rendered, never thrown by this attribute.</remarks>
[AttributeUsage(AttributeTargets.Method)]
public class StepTextAttribute : Attribute
{
    public string? Text { get; }

    /// <summary>Literal display replacements in <c>parameter=replacement</c> form.</summary>
    public IReadOnlyList<string> ParameterOverrides { get; }
    internal bool HasNullOverrideArray { get; }

    public StepTextAttribute(string? stepText) : this(stepText, Array.Empty<string>()) { }

    public StepTextAttribute(string? stepText, string parameterOverride) : this(stepText, new[] { parameterOverride }) { }

    public StepTextAttribute(string? stepText, string[] parameterOverrides)
    {
        Text = stepText;
        HasNullOverrideArray = parameterOverrides == null;
        ParameterOverrides = Array.AsReadOnly((parameterOverrides ?? Array.Empty<string>()).ToArray());
    }
}
