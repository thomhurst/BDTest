using BDTest.Attributes;
using BDTest.Constants;
using BDTest.Test;
using BDTest.Tests.Helpers;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[NotInParallel]
public class StoryTextLookupTests
{
    public static IEnumerable<(BDTestBase test, string? role)> StoryCases()
    {
        yield return (new UnannotatedTest(), null);
        yield return (new DirectStoryTest(), "direct");
        yield return (new InheritedStoryTest(), "direct");
        yield return (new OuterStory.NestedTest(), "outer");
        yield return (new OuterStory.UnannotatedMiddle.NestedTest(), "outer");
        yield return (new OuterStory.InnerStory.NestedTest(), "inner");
        yield return (new OuterStory.DirectStoryTest(), "nested");
        yield return (new OuterStory.InheritedStoryTest(), "direct");
    }

    [Test]
    [MethodDataSource(nameof(StoryCases))]
    public async Task ScenarioAndReportUseTheSameStoryAsTheTest(BDTestBase test, string? role)
    {
        using var ownedFixture = (IDisposable)test;
        var article = role is "outer" or "inner" ? "an" : "a";
        var expected = role == null
            ? null
            : $"As {article} {role}{Environment.NewLine}I want consistent stories{Environment.NewLine}So that reports match{Environment.NewLine}";

        var scenario = await test.When(() => Step()).Then(() => Step()).BDTestAsync();
        var reportedStory = JsonHelper.GetTestDynamicJsonObject(test.TestRun).SelectToken("$.scenarios[0].storyText");

        using (Assert.Multiple())
        {
            await Assert.That(test.GetStoryText()).IsEqualTo(expected);
            await Assert.That(scenario.GetStoryText()).IsEqualTo(expected ?? DefaultValues.StoryTextNotDefined);
            await Assert.That(reportedStory).IsNotNull();
            if (expected == null)
            {
                await Assert.That(Required(reportedStory).Type).IsEqualTo(JTokenType.Null);
            }
            else
            {
                await Assert.That(Required(reportedStory).ToString()).IsEqualTo(expected);
            }

        }
    }

    private static void Step() { }

    private class UnannotatedTest : RunTestBase { }

    [Story(AsA = "direct", IWant = "consistent stories", SoThat = "reports match")]
    private class DirectStoryTest : RunTestBase { }

    private class InheritedStoryTest : DirectStoryTest { }

    [Story(AsA = "outer", IWant = "consistent stories", SoThat = "reports match")]
    private class OuterStory
    {
        public class NestedTest : RunTestBase { }

        public class UnannotatedMiddle
        {
            public class NestedTest : RunTestBase { }
        }

        [Story(AsA = "inner", IWant = "consistent stories", SoThat = "reports match")]
        public class InnerStory
        {
            public class NestedTest : RunTestBase { }
        }

        [Story(AsA = "nested", IWant = "consistent stories", SoThat = "reports match")]
        public class DirectStoryTest : RunTestBase { }

        public class InheritedStoryTest : StoryTextLookupTests.DirectStoryTest { }
    }
}
