using System.Linq;
using BDTest.Settings;
using BDTest.Test;
using NUnit.Framework;
using NUnitTestContext = NUnit.Framework.TestContext;

namespace BDTest.NUnit
{
    [TestFixture]
    public abstract class NUnitBDTestBase<TContext> : AbstractContextBDTestBase<TContext> where TContext : class, new()
    {
        protected override string BDTestExecutionId => NUnitTestContext.CurrentContext.Test.ID;

        [OneTimeSetUp]
        public void SetupNUnitExceptions()
        {
            if (!BDTestSettings.CustomExceptionSettings.SuccessExceptionTypes.Contains(typeof(SuccessException)))
            {
                BDTestSettings.CustomExceptionSettings.SuccessExceptionTypes.Add(typeof(SuccessException));
            }
            
            if (!BDTestSettings.CustomExceptionSettings.InconclusiveExceptionTypes.Contains(typeof(IgnoreException)))
            {
                BDTestSettings.CustomExceptionSettings.InconclusiveExceptionTypes.Add(typeof(IgnoreException));
            }
            
            if (!BDTestSettings.CustomExceptionSettings.InconclusiveExceptionTypes.Contains(typeof(InconclusiveException)))
            {
                BDTestSettings.CustomExceptionSettings.InconclusiveExceptionTypes.Add(typeof(InconclusiveException));
            }
        }

        [TearDown]
        public void PruneContext()
        {
            RemoveContext();
        }

        [TearDown]
        protected override void MarkTestAsComplete()
        {
            base.MarkTestAsComplete();
        }
    }
}
