using System.Text.Json.Serialization;

namespace BDTest;

/// <summary>A structured step value retained separately from inline step text.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(StepTable), "table")]
[JsonDerivedType(typeof(StepDocString), "docString")]
public abstract class StepArgument
{
    private protected StepArgument() { }

    /// <summary>Copies this structured value to an immutable result.</summary>
    public Contracts.StepArgument ToResult() => Helpers.ResultSnapshot.Create(this);
}
