using System.Collections.Generic;
using System.Linq;
using BDTest.Test;

namespace BDTest.NetCore.Razor.ReportMiddleware.Models
{
    public class GroupedExceptions
    {
        private readonly List<ScenarioException> _scenarioExceptions = new List<ScenarioException>();
        public IList<IGrouping<string, ScenarioException>> GroupedScenarioExceptions => _scenarioExceptions
            .GroupBy(scenario => scenario.ExceptionMessage.Message)
            .OrderByDescending(scenarios => scenarios.Count())
            .ToList();

        public GroupedExceptions(List<Scenario> scenarios)
        {
            foreach (var scenario in scenarios.Where(scenario => scenario.Exception != null))
            {
                _scenarioExceptions.Add(new ScenarioException(scenario.Guid, scenario.Exception));
            }
        }
    }

    public class ScenarioException
    {
        public ScenarioException(string testGuid, ExceptionWrapper exceptionMessage)
        {
            TestGuid = testGuid;
            ExceptionMessage = exceptionMessage;
        }

        public string TestGuid { get; }
        public ExceptionWrapper ExceptionMessage { get; }
    }
}