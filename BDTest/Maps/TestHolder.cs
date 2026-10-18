using System.Collections.Concurrent;
using BDTest.Test;

namespace BDTest.Maps;

// Compatibility facade for the default run while adapters migrate to explicit ownership.
internal static class TestHolder
{
    private static TestRunStore Store => BDTestRun.Default.Store;
    internal static string CurrentReportId => BDTestRun.Default.ReportId;
    internal static ConcurrentDictionary<string, BuildableTest> NotRun => Store.NotRun;
    internal static ConcurrentDictionary<string, Scenario> ScenariosByInternalId => Store.ScenariosByInternalId;
    internal static ConcurrentDictionary<string, Scenario> ScenariosByTestFrameworkId => Store.ScenariosByTestFrameworkId;
    internal static void AppendStartupOutput(string id, char value) => Store.AppendStartupOutput(id, value);
    internal static void AppendStartupOutput(string id, string text, bool appendNewLine) => Store.AppendStartupOutput(id, text, appendNewLine);
    internal static void ListenForScenario(string id, Action<Scenario> listener) => Store.ListenForScenario(id, listener);
    internal static void AddScenario(Scenario scenario) => Store.AddScenario(scenario);
    internal static void Clear() => Store.Clear();
    internal static void CompleteTest(string id) => Store.CompleteTest(id);
}
