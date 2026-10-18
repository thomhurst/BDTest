using System.Text.Json.Serialization;

namespace BDTest.Contracts;

/// <summary>A structured value captured for a step.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(StepTable), "table")]
[JsonDerivedType(typeof(StepDocString), "docString")]
public abstract class StepArgument
{
    private protected StepArgument() { }
}
