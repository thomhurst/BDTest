using BDTest.Attributes;
using BDTest.Test;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[Story(AsA = "BDTest developer",
    IWant = "to make sure that BDTest works with parameterised values",
    SoThat = "tests can be written concisely")]
public class ParameterisedTests : RunTestBase
{
    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(4)]
    [Arguments(5)]
    public async Task Test(int integer)
    {
        await When(() => Console.WriteLine(integer))
            .Then(() => Console.WriteLine(integer))
            .BDTestAsync();
    }
}
