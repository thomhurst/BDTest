using System.Net.Http.Headers;
using BDTest.Interfaces;
using BDTest.Test;

namespace BDTest.Output;

internal sealed class AttachmentWriter(BDTestRun run, string frameworkExecutionId, Action ensureExecutionActive) : IBDTestAttachments
{
    public void Attach(string name, byte[] content, string mimeType)
    {
        if (content == null) throw new ArgumentNullException(nameof(content));
        using var stream = new MemoryStream(content, writable: false);
        Attach(name, stream, mimeType);
    }

    public void Attach(string name, Stream content, string mimeType)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255
            || name.Any(character => char.IsControl(character) || character is '/' or '\\'))
            throw new ArgumentException("An attachment name must contain 1-255 characters without control characters or path separators.", nameof(name));
        if (content == null) throw new ArgumentNullException(nameof(content));
        if (!content.CanRead) throw new ArgumentException("The attachment stream must be readable.", nameof(content));
        if (mimeType == null || mimeType.Length > 256 || mimeType.Any(char.IsControl)
            || !MediaTypeHeaderValue.TryParse(mimeType, out var mediaType) || mediaType.MediaType == null || mediaType.MediaType.Contains("*")
            || mediaType.ToString().Length > 256)
            throw new ArgumentException("A concrete MIME type is required.", nameof(mimeType));

        Action<Attachment> publish;
        long generation;
        lock (run.Store.Gate)
        {
            run.ThrowIfDisposed();
            ensureExecutionActive();
            publish = GetPublisher();
            generation = run.AttachmentFiles.Generation;
        }
        var attachment = run.AttachmentFiles.Save(name, content, mediaType.ToString(), generation);
        var attachmentId = attachment.Id ?? throw new InvalidOperationException("The attachment has no ID.");
        try
        {
            lock (run.Store.Gate)
            {
                run.ThrowIfDisposed();
                ensureExecutionActive();
                run.AttachmentFiles.EnsureAvailable(attachmentId);
                publish(attachment);
            }
        }
        catch
        {
            run.AttachmentFiles.Remove(attachmentId);
            throw;
        }
    }

    private Action<Attachment> GetPublisher()
    {
        var scope = AttachmentScope.Current;
        if (scope?.Scenario.Run == run && scope.Scenario.FrameworkTestId == frameworkExecutionId)
        {
            scope.ThrowIfInactive();
            return attachment =>
            {
                scope.ThrowIfInactive();
                attachment.AttemptNumber = scope.AttemptNumber;
                scope.Step.ExecutionAttachments.Add(attachment);
            };
        }
        var construction = TestOutputData.ConstructionOutput;
        if (construction?.Run == run && construction.FrameworkExecutionId == frameworkExecutionId)
        {
            ThrowIfConstructionCompleted();
            return attachment =>
            {
                ThrowIfConstructionCompleted();
                attachment.AttemptNumber = 1;
                construction.Attachments.Add(attachment);
            };

            void ThrowIfConstructionCompleted()
            {
                if (construction.AttachmentCaptureCompleted)
                    throw new InvalidOperationException("The attachment's example factory has already completed.");
            }
        }
        return attachment => run.Store.ListenForScenario(frameworkExecutionId, scenario =>
        {
            // Example rows share the framework ID, but setup/teardown owns the outline.
            if (scenario.OutlineId != null)
                scenario = run.Store.ScenariosByInternalId[scenario.OutlineId];
            attachment.AttemptNumber = scenario.RetryCount + 1;
            scenario.ExecutionAttachments.Add(attachment);
        }, () => run.AttachmentFiles.Remove(attachment.Id ?? throw new InvalidOperationException("The attachment has no ID.")));
    }

    public void AttachFile(string path, string? mimeType = null)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A file path is required.", nameof(path));
        using var stream = File.OpenRead(path);
        Attach(Path.GetFileName(path), stream, mimeType ?? "application/octet-stream");
    }
}
