using System.Text.Json.Serialization;

namespace BDTest;

/// <summary>A structured step value retained separately from inline step text.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(StepTable), "table")]
[JsonDerivedType(typeof(StepDocString), "docString")]
public abstract class StepArgument
{
    private protected StepArgument() { }
}
