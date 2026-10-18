using System.Text.Json.Serialization;

namespace BDTest.Test;

/// <summary>A multiline step value with an optional content type hint, such as application/json.</summary>
public sealed class StepDocString : StepArgument
{
    public string Content { get; }
    public string ContentType { get; }

    [JsonConstructor]
    public StepDocString(string content, string contentType = null)
    {
        Content = content ?? throw new ArgumentNullException(nameof(content));
        ContentType = contentType;
    }

    public override string ToString() => string.IsNullOrWhiteSpace(ContentType) ? "(doc string)" : $"({ContentType})";
}
