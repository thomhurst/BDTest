using System;
using System.Collections.Generic;
using System.Linq;
using BDTest.Extensions;
using BDTest.Helpers;
using BDTest.Maps;
using BDTest.Output;

namespace BDTest.Test.Steps
{
    public abstract class StepBuilder : BuildableTest
    {
        internal readonly List<Step> ExistingSteps;
        protected abstract StepType StepType { get; }

        static StepBuilder()
        {
            Initialiser.Initialise();
        }

        protected T WithStepText<T>(Func<string> overridingStepText) where T : StepBuilder
        {
            ExistingSteps.Last().OverriddenStepText = overridingStepText;
            return (T) this;
        }

        // First Test Construction - Given or When
        internal StepBuilder(Runnable runnable, string callerMember, string callerFile, string testId, string reportId, StepType stepType, BDTestBase bdTestBase)
        {
            BdTestBase = bdTestBase;
            CallerMember = callerMember;
            CallerFile = callerFile;
            TestId = testId;
            ReportId = reportId;

            var testGuid = System.Guid.NewGuid();
            Guid = testGuid.ToString();
            TestOutputData.TestId = testGuid;
            
            ExistingSteps = new List<Step> { new Step(runnable, stepType, Guid, ReportId) };
            
            TestHolder.NotRun[testGuid.ToString()] = this;
            
            StoryText = StoryTextHelper.GetStoryText(bdTestBase);
            SetScenarioText();
            CustomTestInformation = TestInformationAttributeHelper.GetTestInformationAttributes();
        }

        internal StepBuilder(List<Step> previousSteps, Runnable runnable, BuildableTest previousPartiallyBuiltTest)
        {
            StoryText = previousPartiallyBuiltTest.StoryText;
            ScenarioText = previousPartiallyBuiltTest.ScenarioText;
            BdTestBase = previousPartiallyBuiltTest.BdTestBase;
            CallerMember = previousPartiallyBuiltTest.CallerMember;
            CallerFile = previousPartiallyBuiltTest.CallerFile;
            TestId = previousPartiallyBuiltTest.TestId;
            Guid = previousPartiallyBuiltTest.Guid;
            CustomTestInformation = previousPartiallyBuiltTest.CustomTestInformation;

            TestHolder.NotRun[previousPartiallyBuiltTest.Guid] = this;

            ExistingSteps = previousSteps;
            ExistingSteps.Add(new Step(runnable, StepType, Guid, ReportId));
        }
        
        private void SetScenarioText()
        {
            var scenarioTextWithParameters = ScenarioTextHelper.GetScenarioTextWithParameters(this.GetTestMethod(), CallerMember);
            ScenarioText = scenarioTextWithParameters.ScenarioText;
            Parameters = scenarioTextWithParameters.Parameters;
        }
    }
}
