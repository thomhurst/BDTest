using BDTest.Attributes;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[Story(AsA = "BDTest developer",
    IWant = "to make sure that BDTest works with parameterised values",
    SoThat = "tests can be written concisely")]
public class ParameterisedTests : RunTestBase
{
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    public async Task Test(int integer)
    {
        await When(() => Console.WriteLine(integer))
            .Then(() => Console.WriteLine(integer))
            .BDTestAsync();
    }
}
