namespace BDTest.Interfaces;

/// <summary>Captures binary content for the current step, or for its scenario outside a step.</summary>
public interface IBDTestAttachments
{
    void Attach(string name, byte[] content, string mimeType);

    /// <summary>Copies from the current position without closing the caller's stream.</summary>
    void Attach(string name, Stream content, string mimeType);

    /// <summary>Copies a file. An omitted MIME type uses application/octet-stream.</summary>
    void AttachFile(string path, string mimeType = null);
}
