using BDTest.NUnit;
using BDTest.TestUtilities;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class ExceptionRegistrationTests
{
    [TestCase(false)]
    [TestCase(true)]
    public Task ConcurrentSetupAcrossContextTypesRegistersExceptionsOnce(bool preRegistered) =>
        ExceptionRegistrationProbe.Verify(typeof(NUnitBDTestBase<>), "SetupNUnitExceptions", preRegistered,
            ("SuccessExceptionTypes", typeof(SuccessException)),
            ("SkipExceptionTypes", typeof(IgnoreException)),
            ("InconclusiveExceptionTypes", typeof(InconclusiveException)));
}
