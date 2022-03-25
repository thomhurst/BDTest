using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using BDTest.Test;

namespace BDTest.Maps;

internal static class TestHolder
{
    internal static readonly string CurrentReportId = Guid.NewGuid().ToString("N");
    internal static ConcurrentDictionary<string, BuildableTest> NotRun { get; } = new();
    internal static ConcurrentDictionary<string, Scenario> ScenariosByInternalId { get; } = new();
    internal static ConcurrentDictionary<string, Scenario> ScenariosByTestFrameworkId { get; } = new();
        
    private static readonly ConcurrentDictionary<string, List<Action<Scenario>>> ScenarioListeners = new();

    internal static void ListenForScenario(string testId, Action<Scenario> @delegate)
    {
        var list = ScenarioListeners.GetOrAdd(testId, new List<Action<Scenario>>());
        list.Add(@delegate);
    }

    internal static void AddScenario(Scenario scenario)
    {
        ScenariosByInternalId[scenario.Guid] = scenario;
        ScenariosByTestFrameworkId[scenario.FrameworkTestId] = scenario;
            
        if (ScenarioListeners.TryGetValue(scenario.FrameworkTestId, out var delegateList))
        {
            foreach (var @delegate in delegateList.ToList())
            {
                @delegate(scenario);
                delegateList.Remove(@delegate);
            }
        }
    }
}