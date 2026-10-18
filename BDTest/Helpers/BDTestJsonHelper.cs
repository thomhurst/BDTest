using BDTest.Maps;
using BDTest.Output;
using BDTest.Settings;
using BDTest.Test;
using Newtonsoft.Json;

namespace BDTest.Helpers;

public static class BDTestJsonHelper
{
    public static string GetTestJsonData()
    {
        return GetTestJsonData(new BDTestRunDescriptor());
    }

    public static string GetTestJsonData(BDTestRunDescriptor bdTestRunDescriptor) => GetTestJsonData(bdTestRunDescriptor, BDTestRun.Default);

    /// <summary>Creates a report containing only the supplied run's scenarios and pending definitions.</summary>
    public static string GetTestJsonData(BDTestRunDescriptor bdTestRunDescriptor, BDTestRun run)
    {
        if (run == null) throw new ArgumentNullException(nameof(run));
        run.ThrowIfDisposed();
        var scenarios = BDTestUtil.GetScenarios(run);
            
        var testTimer = GetTestTimer(scenarios, run);
            
        var dataToOutput = new BDTestOutputModel
        {
            Id = run.ReportId,
            Environment = bdTestRunDescriptor?.Environment ?? BDTestSettings.LegacyEnvironment,
            Tag = bdTestRunDescriptor?.Tag ?? BDTestSettings.LegacyTag,
            BranchName = bdTestRunDescriptor?.BranchName ?? BDTestSettings.LegacyBranchName,
            MachineName = Environment.MachineName,
            Scenarios = scenarios,
            TestTimer = testTimer,
            NotRun = BDTestUtil.GetNotRunScenarios(run),
            Version = BDTestVersionHelper.CurrentVersion
        };
            
        return Serialize(dataToOutput, Formatting.Indented);
    }

    internal static string Serialize(BDTestOutputModel model, Formatting formatting = Formatting.None)
    {
        var settings = new JsonSerializerSettings
        {
            PreserveReferencesHandling = PreserveReferencesHandling.Objects
        };

        return JsonConvert.SerializeObject(model, formatting, settings);
    }
        
    private static TestTimer GetTestTimer(IEnumerable<Scenario> scenarios, BDTestRun run)
    {
        var enumerable = scenarios.ToList();

        if (enumerable.Count == 0)
        {
            return new TestTimer { TestsStartedAt = run.StartedAt };
        }

        var testTimer = new TestTimer
        {
            TestsStartedAt = enumerable.GetStartDateTime(),
            TestsFinishedAt = enumerable.GetEndDateTime()
        };

        return testTimer;
    }
}
