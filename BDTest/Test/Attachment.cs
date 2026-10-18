using System.Text.Json.Serialization;

namespace BDTest.Test;

/// <summary>Report metadata for a captured file. Content and local paths are never serialized.</summary>
public sealed class Attachment
{
    [JsonInclude] public string? Id { get; internal set; }
    [JsonInclude] public string? Name { get; internal set; }
    [JsonInclude] public string? MimeType { get; internal set; }
    [JsonInclude] public long Length { get; internal set; }

    /// <summary>The owning scenario's one-based attempt number.</summary>
    [JsonInclude] public int AttemptNumber { get; internal set; }

    /// <summary>The remote content location, when supplied by an uploader.</summary>
    [JsonInclude] public string? Uri { get; internal set; }
}
