[assembly: BDTest.BDTestConfiguration(typeof(BDTest.TUnit.Tests.AssemblyConfiguration))]

namespace BDTest.TUnit.Tests;

public sealed class AssemblyConfiguration : IBDTestConfiguration
{
    public void Configure(BDTestOptions options) => SkipOutcomeTests.Configure(options);
}
