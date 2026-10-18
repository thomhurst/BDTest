using BDTest.TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BDTest.MSTest.Tests;

[TestClass]
[DoNotParallelize]
public class ExceptionRegistrationTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ConcurrentSetupAcrossContextTypesRegistersExceptionsOnce(bool preRegistered)
    {
        // InitializeBDTest installs the isolated core's console interceptor.
        var output = Console.Out;
        try
        {
            await ExceptionRegistrationProbe.Verify(typeof(RegistrationFixture<>), "RegisterExceptions", preRegistered,
                ("InconclusiveExceptionTypes", typeof(AssertInconclusiveException)));
        }
        finally
        {
            Console.SetOut(output);
        }
    }

    public sealed class RegistrationFixture<TContext> : MSTestBDTestBase<TContext>
        where TContext : class, new()
    {
        public void RegisterExceptions()
        {
            try
            {
                InitializeBDTest();
            }
            finally
            {
                TearDown();
            }
        }
    }
}
