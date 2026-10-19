using BDTest;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NUnitPreparationRetryTests : NUnitBDTestBase<NUnitPreparationRetryTests.Resource>
{
    private string? _phase;
    private int _configurations;
    private int _initializations;

    protected override ValueTask<Resource> CreateContextAsync(ContextCreation creation) =>
        new(new Resource(() =>
        {
            if (_phase == "initialize" && ++_initializations == 1)
                Assert.Fail("First initialization fails");
        }));

    protected override void ConfigureContext(BDTestContext<Resource> context)
    {
        if (_phase == "configure" && ++_configurations == 1)
            Assert.Fail("First configuration fails");
    }

    [TestCase("configure")]
    [TestCase("initialize")]
    [BDTestRetry(1)]
    public async Task PreparationAssertionCanRetrySuccessfully(string phase)
    {
        _phase = phase;
        var scenario = await When(() => Complete()).Then(() => Complete()).BDTestAsync();
        Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
        Assert.That(scenario.RetryCount, Is.EqualTo(1));
        Assert.That(Required(scenario.Attempts).Select(attempt => attempt.Status),
            Is.EqualTo(new[] { Status.Failed, Status.Passed }));
    }

    private static void Complete() { }

    public sealed class Resource(Action initialize) : IBDTestContextLifetime
    {
        public async ValueTask InitializeAsync()
        {
            await Task.Yield();
            initialize();
        }
    }
}
