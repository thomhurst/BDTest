[assembly: BDTest.BDTestConfiguration(typeof(BDTest.MSTest.Tests.AssemblyConfiguration))]

namespace BDTest.MSTest.Tests;

public sealed class AssemblyConfiguration : IBDTestConfiguration
{
    public void Configure(BDTestOptions options) => SkipOutcomeTests.Configure(options);
}
