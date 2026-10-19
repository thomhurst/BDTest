using System.Collections.ObjectModel;

namespace BDTest.Rendering;

/// <summary>A compact failure-group display projection with no exception or scenario graph.</summary>
public sealed class ReportFailureOverview
{
    public string TypeName { get; }
    public string Message { get; }
    public string? Location { get; }
    public int ScenarioCount { get; }
    public IReadOnlyList<ReportFailureLink> Scenarios { get; }

    public ReportFailureOverview(string typeName, string message, string? location, int scenarioCount,
        IReadOnlyList<ReportFailureLink> scenarios)
    {
        TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Location = location;
        if (scenarioCount < 0) throw new ArgumentOutOfRangeException(nameof(scenarioCount));
        ScenarioCount = scenarioCount;
        if (scenarios == null) throw new ArgumentNullException(nameof(scenarios));
        Scenarios = new ReadOnlyCollection<ReportFailureLink>(scenarios.ToArray());
    }
}

/// <summary>A failure's navigation label and optional display details, without its source scenario.</summary>
public sealed class ReportFailureLink
{
    public string ScenarioId { get; }
    public string Name { get; }
    public string? Description { get; }
    public TimeSpan? Duration { get; }

    public ReportFailureLink(string scenarioId, string name, string? description = null, TimeSpan? duration = null)
    {
        ScenarioId = scenarioId ?? throw new ArgumentNullException(nameof(scenarioId));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description;
        Duration = duration;
    }
}
