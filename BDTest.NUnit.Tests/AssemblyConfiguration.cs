[assembly: BDTest.BDTestConfiguration(typeof(BDTest.Tests.AssemblyConfiguration))]

namespace BDTest.Tests;

public sealed class AssemblyConfiguration : IBDTestConfiguration
{
    public void Configure(BDTestOptions options) => Fixtures.SkipOutcomeTests.Configure(options);
}
