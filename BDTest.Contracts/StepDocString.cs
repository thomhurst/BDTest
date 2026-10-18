using System.Text.Json.Serialization;

namespace BDTest.Contracts;

/// <summary>A multiline step value with an optional content type.</summary>
public sealed class StepDocString : StepArgument
{
    public string Content { get; }
    public string? ContentType { get; }

    [JsonConstructor]
    public StepDocString(string content, string? contentType = null)
    {
        Content = content ?? throw new ArgumentNullException(nameof(content));
        ContentType = contentType;
    }
}
