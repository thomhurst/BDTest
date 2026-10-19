using BDTest.Contracts;

namespace BDTest.Razor.Reports.Helpers;

internal static class BDTestAttachmentHelper
{
    public static IEnumerable<AttachmentInfo> GetAttachments(BDTestReport report)
    {
        var pending = new Stack<ScenarioResult>(report.Scenarios);
        while (pending.Count != 0)
        {
            var scenario = pending.Pop();
            // Duplicate declarations must reach manifest validation. Only an attempt's snapshot
            // of the same step attachment may repeat metadata already retained on the final step.
            foreach (var attachment in scenario.Attachments) yield return attachment;
            var finalAttachments = new Dictionary<(string? StepId, string Id), AttachmentInfo>();
            foreach (var step in scenario.Steps)
                foreach (var attachment in step.Attachments)
                {
                    finalAttachments[(step.Guid, attachment.Id)] = attachment;
                    yield return attachment;
                }
            foreach (var attempt in scenario.Attempts)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var step in attempt.Steps)
                    foreach (var attachment in step.Attachments)
                    {
                        var repeatedSnapshot = seen.Add(attachment.Id)
                            && finalAttachments.TryGetValue((step.Guid, attachment.Id), out var previous)
                            && Same(previous, attachment);
                        if (!repeatedSnapshot) yield return attachment;
                    }
            }
            foreach (var example in scenario.Examples) pending.Push(example);
        }
    }

    private static bool Same(AttachmentInfo left, AttachmentInfo right) =>
        left.Id == right.Id && left.Name == right.Name && left.MimeType == right.MimeType
        && left.Length == right.Length && left.AttemptNumber == right.AttemptNumber && left.Uri == right.Uri;
}
