namespace BDTest.Razor.Reports.Storage;

/// <summary>Shared identifier and bounded stream validation for attachment stores.</summary>
public static class AttachmentStorage
{
    public static bool IsValidId(string id) => id?.Length == 32 && id.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    public static void ValidateIds(string reportId, string attachmentId = null)
    {
        if (!IsValidId(reportId)) throw new ArgumentException("A lowercase 32-character hexadecimal report ID is required.", nameof(reportId));
        if (attachmentId != null && !IsValidId(attachmentId)) throw new ArgumentException("A lowercase 32-character hexadecimal attachment ID is required.", nameof(attachmentId));
    }

    /// <summary>Copies exactly the declared length with bounded memory, rejecting short and overlong input.</summary>
    public static async Task CopyExactlyAsync(Stream source, Stream destination, long length, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        var buffer = new byte[81920];
        var remaining = length;
        while (remaining > 0)
        {
            var count = await source.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), cancellationToken);
            if (count == 0) throw new InvalidDataException("Attachment content is shorter than its recorded length.");
            await destination.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
            remaining -= count;
        }
        if (await source.ReadAsync(buffer.AsMemory(0, 1), cancellationToken) != 0)
            throw new InvalidDataException("Attachment content exceeds its recorded length.");
    }
}
