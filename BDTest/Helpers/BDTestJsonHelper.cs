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

    public static string GetTestJsonData(BDTestRunDescriptor bdTestRunDescriptor)
    {
        var scenarios = BDTestUtil.GetScenarios();
            
        var testTimer = GetTestTimer(scenarios);
            
        var dataToOutput = new BDTestOutputModel
        {
            Id = BDTestUtil.GetCurrentReportId,
            Environment = bdTestRunDescriptor?.Environment ?? BDTestSettings.LegacyEnvironment,
            Tag = bdTestRunDescriptor?.Tag ?? BDTestSettings.LegacyTag,
            BranchName = bdTestRunDescriptor?.BranchName ?? BDTestSettings.LegacyBranchName,
            MachineName = Environment.MachineName,
            Scenarios = scenarios,
            TestTimer = testTimer,
            NotRun = BDTestUtil.GetNotRunScenarios(),
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
        
    private static TestTimer GetTestTimer(IEnumerable<Scenario> scenarios)
    {
        var enumerable = scenarios.ToList();

        if (enumerable.Count == 0)
        {
            return new TestTimer();
        }

        var testTimer = new TestTimer
        {
            TestsStartedAt = enumerable.GetStartDateTime(),
            TestsFinishedAt = enumerable.GetEndDateTime()
        };

        return testTimer;
    }
}
