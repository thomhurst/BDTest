using BDTest;
using BDTest.Test;

namespace BDTest.Tests.Fixtures;

[Story(AsA = "BDTest developer",
    IWant = "to make sure that test contexts are different for each test",
    SoThat = "tests don't share state")]
public class InlineContextTests : RunTestBase
{
    [Test]
    public Task TestWithInlineContext1() => EnsureContextsAreUnique();

    [Test]
    public Task TestWithInlineContext2() => EnsureContextsAreUnique();

    private async Task EnsureContextsAreUnique()
    {
        var first = new MyTestContext();
        var second = new MyTestContext();
        await When(() => RecordContext(first))
            .Then(() => RecordContext(second))
            .BDTestAsync();
        await Assert.That(first.Id).IsNotEqualTo(second.Id);
    }

    private static void RecordContext(MyTestContext context) => Console.WriteLine(context.Id);
}
