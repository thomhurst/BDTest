using BDTest.NUnit;
using BDTest.Settings;
using NUnit.Framework;

namespace BDTest.Tests.Helpers;

// Dedicated compatibility harness until #954 removes the legacy core APIs.
// Native adapter lifecycle tests continue using NUnitBDTestBase directly.
public abstract class LegacyNUnitBDTestBase<TContext> : NUnitBDTestBase<TContext> where TContext : class
{
    public override BDTestRun TestRun => BDTestRun.Default;

    [OneTimeSetUp]
    public static void RegisterLegacyOutcomes() =>
        BDTestSettings.CustomExceptionSettings.RegisterFrameworkOutcomes(OutcomeMap);
}
