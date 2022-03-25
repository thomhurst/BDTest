using System.Net;
using System.Threading.Tasks;
using BDTest.Attributes;
using NUnit.Framework;

namespace BDTest.Example;

[Story(AsA = "BDTest author",
    IWant = "to show an example of how to use the framework",
    SoThat = "people can get set up easier")]
public class ExampleTest : MyTestBase
{
    [Test]
    [ScenarioText("A passing test using steps defined in a BDTestBase, with StoryText, ScenarioText and StepTexts")]
    public async Task TestPass()
    {
        await When(() => Steps.CreateAnAccount())
            .Then(() => Assertions.TheHttpStatusCodeIs(HttpStatusCode.NotFound))
            .BDTestAsync();
    }
        
    [Test]
    [BugInformation("123456")]
    [ScenarioText("A failing test using steps defined in a BDTestBase, with StoryText, ScenarioText and StepTexts")]
    public async Task TestFail()
    {
        await When(() => Steps.CreateAnAccount())
            .Then(() => Assertions.TheHttpStatusCodeIs(HttpStatusCode.OK))
            .BDTestAsync();
    }
}