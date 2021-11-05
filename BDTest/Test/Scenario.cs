using System;
using System.Collections.Generic;
using System.Linq;
using BDTest.Attributes;
using BDTest.Constants;
using BDTest.Maps;
using BDTest.Output;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace BDTest.Test
{
    public class Scenario
    {
        [JsonProperty] public string Guid { get; private set; }
        [JsonProperty] public string ReportId { get; private set; }
        [JsonProperty] public DateTime StartTime { get; internal set; }

        [JsonProperty] public DateTime EndTime { get; internal set; }

        [JsonProperty] public string FileName { get; private set; }
        
        [JsonProperty]
        public string TestStartupInformation { get; internal set; }

        [JsonIgnore] public ExceptionWrapper Exception => Steps.Select(step => step.Exception).FirstOrDefault(exception => exception != null);
        
        [JsonProperty] public string TearDownOutput { get; internal set; }
        
        [JsonProperty] public string CustomHtmlOutputForReport { get; internal set; }

        [JsonIgnore] internal BDTestRuntimeInformation RuntimeInformation { get; set; }

        [JsonConstructor]
        private Scenario()
        {
        }

        internal bool AlreadyExecuted;

        internal Scenario(List<Step> steps, BuildableTest testDetails)
        {
            Guid = testDetails.Guid;
            FrameworkTestId = testDetails.TestId;

            RuntimeInformation = new BDTestRuntimeInformation
            {
                CallerFile = testDetails.CallerFile,
                CallerMember = testDetails.CallerMember,
                BdTestBase = testDetails.BdTestBase
            };
            
            TestHolder.NotRun.TryRemove(Guid, out _);
            TestHolder.AddScenario(this);

            StoryText = testDetails.StoryText;
            ScenarioText = testDetails.ScenarioText;
            CustomTestInformation = testDetails.CustomTestInformation;

            FileName = testDetails.CallerFile;
            
            Steps = steps;
        }

        [JsonProperty]
        public string FrameworkTestId { get; private set; }

        [JsonProperty] public List<Step> Steps { get; private set; }

        [JsonProperty] public string Output { get; internal set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty]
        public Status Status { get; internal set; } = Status.Inconclusive;

        [JsonProperty] internal StoryText StoryText { get; private set; }

        [JsonProperty] internal ScenarioText ScenarioText { get; private set; }
        
        [JsonProperty] public TestInformationAttribute[] CustomTestInformation { get; private set; }
        
        [JsonIgnore] internal bool ShouldRetry { get; set; }
        [JsonProperty] public int RetryCount { get; internal set; }
        [JsonIgnore] public BDTestBase BdTestBaseClass => RuntimeInformation.BdTestBase;

        public string GetScenarioText()
        {
            return ScenarioText?.Scenario ?? "Scenario Title Not Defined";
        }

        public string GetStoryText()
        {
            return StoryText?.Story ?? DefaultValues.StoryTextNotDefined;
        }

        [JsonConverter(typeof(TimespanConverter))]
        [JsonProperty]
        public TimeSpan TimeTaken { get; internal set; }
    }
}
