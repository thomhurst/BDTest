using BDTest.Maps;
using BDTest.Test;

namespace BDTest.Helpers;

/// <summary>Enumerates report attachment metadata, including outline examples and all retained attempts.</summary>
public static class BDTestAttachmentHelper
{
    public static IEnumerable<Contracts.AttachmentInfo> GetAttachments(Contracts.BDTestReport report)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));
        var pending = new Stack<Contracts.ScenarioResult>(report.Scenarios);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        while (pending.Count != 0)
        {
            var scenario = pending.Pop();
            foreach (var attachment in scenario.Attachments
                .Concat(scenario.Steps.SelectMany(step => step.Attachments))
                .Concat(scenario.Attempts.SelectMany(attempt => attempt.Steps).SelectMany(step => step.Attachments)))
                if (ids.Add(attachment.Id)) yield return attachment;
            foreach (var example in scenario.Examples) pending.Push(example);
        }
    }

    public static IEnumerable<Attachment> GetAttachments(BDTestOutputModel report)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));
        var pending = new Stack<Scenario>(report.Scenarios ?? new List<Scenario>());
        var visited = new HashSet<Scenario>();
        while (pending.Count != 0)
        {
            var scenario = pending.Pop();
            if (scenario == null || !visited.Add(scenario)) continue;
            foreach (var attachment in scenario.Attachments ?? new List<Attachment>()) yield return attachment;
            foreach (var step in scenario.Steps ?? new List<Step>())
                foreach (var attachment in step?.Attachments ?? new List<Attachment>()) yield return attachment;
            foreach (var example in scenario.Examples ?? new List<Scenario>()) pending.Push(example);
        }
    }
}
