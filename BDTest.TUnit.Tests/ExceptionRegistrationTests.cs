using BDTest.TestUtilities;
using TUnit.Core.Exceptions;

namespace BDTest.TUnit.Tests;

public class ExceptionRegistrationTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public Task ConcurrentSetupAcrossContextTypesRegistersExceptionsOnce(bool preRegistered) =>
        ExceptionRegistrationProbe.Verify(typeof(TUnitBDTestBase<>), "SetupTUnitExceptions", preRegistered,
            ("SkipExceptionTypes", typeof(SkipTestException)),
            ("InconclusiveExceptionTypes", typeof(InconclusiveTestException)));
}
