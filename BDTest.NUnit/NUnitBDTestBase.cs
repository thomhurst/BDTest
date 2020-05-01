using System.Linq;
using BDTest.Test;
using NUnit.Framework;
using NUnitTestContext = NUnit.Framework.TestContext;

namespace BDTest.NUnit
{
    [TestFixture]
    public abstract class NUnitBDTestBase<TContext> : AbstractContextBDTestBase<TContext> where TContext : class, new()
    {
        protected override string TestId => NUnitTestContext.CurrentContext.Test.ID;

        [OneTimeSetUp]
        public void AllowAssertPassException()
        {
            if (!BDTestSettings.SuccessExceptionTypes.Contains(typeof(SuccessException)))
            {
                BDTestSettings.SuccessExceptionTypes.Add(typeof(SuccessException));
            }
        }

        [TearDown]
        public void PruneContext()
        {
            RemoveContext();
        }
    }
}
