using System;
using System.Linq;
using System.Runtime.CompilerServices;
using BDTest.Test;
using NUnit.Framework;
using NUnitTestContext = NUnit.Framework.TestContext;

namespace BDTest.NUnit
{
    [TestFixture]
    public class NUnitBDTestBase<TContext> : BDTestBase where TContext : class, new()
    {
        protected NUnitBDTestBase()
        {
            var nunitPackage = AppDomain.CurrentDomain
                .GetAssemblies().FirstOrDefault(it => it.FullName.ToLower().StartsWith("nunit.framework"));
            if (nunitPackage == null)
            {
                throw new Exception("You do not have NUnit installed in order to extend from NUnitBDTestBase!");
            }
        }

        private readonly ConditionalWeakTable<string, TContext> _contexts = new ConditionalWeakTable<string, TContext>();

        [TearDown]
        public void PruneContext()
        {
            _contexts.Remove(NUnitTestContext.CurrentContext.Test.ID);
        }

        public TContext Context
        {
            get
            {
                var testContextId = NUnitTestContext.CurrentContext.Test.ID;
                _contexts.TryGetValue(testContextId, out var context);

                if (context != null)
                {
                    return context;
                }

                context = Activator.CreateInstance<TContext>();
                _contexts.Add(testContextId, context);
                return context;
            }
        }
    }
}
