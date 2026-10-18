using System.Text.Json.Serialization;

namespace BDTest.Contracts;

/// <summary>Captured-file metadata; file content and local paths are excluded.</summary>
public sealed class AttachmentInfo
{
    public string Id { get; }
    public string Name { get; }
    public string MimeType { get; }
    public long Length { get; }
    public int AttemptNumber { get; }
    public string? Uri { get; }

    [JsonConstructor]
    public AttachmentInfo(
        string id,
        string name,
        string mimeType,
        long length,
        int attemptNumber,
        string? uri = null)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        MimeType = mimeType ?? throw new ArgumentNullException(nameof(mimeType));
        Length = length;
        AttemptNumber = attemptNumber;
        Uri = uri;
    }
}
