using BDTest.Attributes;
using BDTest.Constants;
using BDTest.Test;
using BDTest.Tests.Helpers;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
public class StoryTextLookupTests
{
    private static IEnumerable<TestCaseData> StoryCases()
    {
        yield return new TestCaseData(new UnannotatedTest(), null).SetName("No_story_attribute");
        yield return new TestCaseData(new DirectStoryTest(), "direct").SetName("Direct_story_attribute");
        yield return new TestCaseData(new InheritedStoryTest(), "direct").SetName("Inherited_story_attribute");
        yield return new TestCaseData(new OuterStory.NestedTest(), "outer").SetName("Enclosing_story_attribute");
        yield return new TestCaseData(new OuterStory.UnannotatedMiddle.NestedTest(), "outer")
            .SetName("Story_attribute_on_distant_enclosing_type");
        yield return new TestCaseData(new OuterStory.InnerStory.NestedTest(), "inner")
            .SetName("Nearest_enclosing_story_wins");
        yield return new TestCaseData(new OuterStory.DirectStoryTest(), "nested")
            .SetName("Direct_story_overrides_enclosing_story");
        yield return new TestCaseData(new OuterStory.InheritedStoryTest(), "direct")
            .SetName("Inherited_story_overrides_enclosing_story");
    }

    [SetUp]
    public void SetUp() => TestResetHelper.ResetData();

    [TestCaseSource(nameof(StoryCases))]
    public async Task ScenarioAndReportUseTheSameStoryAsTheTest(BDTestBase test, string role)
    {
        var article = role is "outer" or "inner" ? "an" : "a";
        var expected = role == null
            ? null
            : $"As {article} {role}{Environment.NewLine}I want consistent stories{Environment.NewLine}So that reports match{Environment.NewLine}";

        var scenario = await test.When(() => Step()).Then(() => Step()).BDTestAsync();
        var reportedStory = JsonHelper.GetTestDynamicJsonObject().SelectToken("$.Scenarios[0].StoryText");

        Assert.Multiple(() =>
        {
            Assert.That(test.GetStoryText(), Is.EqualTo(expected));
            Assert.That(scenario.GetStoryText(), Is.EqualTo(expected ?? DefaultValues.StoryTextNotDefined));
            Assert.That(reportedStory, Is.Not.Null);
            if (expected == null)
            {
                Assert.That(Required(reportedStory).Type, Is.EqualTo(JTokenType.Null));
            }
            else
            {
                Assert.That(Required(reportedStory).ToString(), Is.EqualTo(expected));
            }
        });
    }

    private static void Step() { }

    private class UnannotatedTest : BDTestBase { }

    [Story(AsA = "direct", IWant = "consistent stories", SoThat = "reports match")]
    private class DirectStoryTest : BDTestBase { }

    private class InheritedStoryTest : DirectStoryTest { }

    [Story(AsA = "outer", IWant = "consistent stories", SoThat = "reports match")]
    private class OuterStory
    {
        public class NestedTest : BDTestBase { }

        public class UnannotatedMiddle
        {
            public class NestedTest : BDTestBase { }
        }

        [Story(AsA = "inner", IWant = "consistent stories", SoThat = "reports match")]
        public class InnerStory
        {
            public class NestedTest : BDTestBase { }
        }

        [Story(AsA = "nested", IWant = "consistent stories", SoThat = "reports match")]
        public class DirectStoryTest : BDTestBase { }

        public class InheritedStoryTest : StoryTextLookupTests.DirectStoryTest { }
    }
}
