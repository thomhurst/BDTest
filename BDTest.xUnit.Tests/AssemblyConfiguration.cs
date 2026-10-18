[assembly: BDTest.BDTestConfiguration(typeof(BDTest.xUnit.Tests.AssemblyConfiguration))]

namespace BDTest.xUnit.Tests;

public sealed class AssemblyConfiguration : IBDTestConfiguration
{
    public void Configure(BDTestOptions options) => SkipOutcomeTests.Configure(options);
}
