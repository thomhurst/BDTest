using BDTest.Maps;
using BDTest.Test;

namespace BDTest.Helpers;

/// <summary>Enumerates report attachment metadata, including outline examples and all retained attempts.</summary>
public static class BDTestAttachmentHelper
{
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
