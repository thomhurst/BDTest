namespace BDTest.Settings;

/// <summary>Byte limits copied when a run starts. Zero permits only empty attachments.</summary>
public sealed class AttachmentOptions
{
    public long MaximumAttachmentBytes { get; set; } = 10 * 1024 * 1024;
    public long MaximumRunBytes { get; set; } = 100 * 1024 * 1024;
}
