using BDTest.Attributes;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[Story(AsA = "BDTest developer",
    IWant = "to make sure that test contexts are different for each test",
    SoThat = "tests don't share state")]
[FixtureLifeCycle(LifeCycle.SingleInstance)]
public class InlineContextTests : RunTestBase
{
    private MyTestContext? _context1;
    private MyTestContext? _context2;

    [Test]
    public async Task TestWithInlineContext1()
    {
        var context = new MyTestContext();
        _context1 = context;
        await When(() => Console.WriteLine())
            .Then(() => Console.WriteLine())
            .BDTestAsync();
    }
        
    [Test]
    public async Task TestWithInlineContext2()
    {
        var context = new MyTestContext();
        _context2 = context;
        await When(() => Console.WriteLine())
            .Then(() => Console.WriteLine())
            .BDTestAsync();
    }
        
    [OneTimeTearDown]
    public void EnsureContextsAreUnique()
    {
        // Filtered runs may omit either context capture test.
        if (_context1 is null || _context2 is null) return;
        Assert.That(_context1.Id, Is.Not.EqualTo(_context2.Id));
    }
}
