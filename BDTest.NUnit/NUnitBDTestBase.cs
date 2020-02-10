using System;
using System.Runtime.CompilerServices;
using BDTest.Test;
using NUnit.Framework;
using NUnitTestContext = NUnit.Framework.TestContext;

namespace BDTest.NUnit
{
    [TestFixture]
    public class NUnitBDTestBase<TContext> : BDTestBase where TContext : class, new()
    {
        protected override string TestId => TestContext.CurrentContext.Test.ID;
        private readonly ConditionalWeakTable<string, TContext> _contexts = new ConditionalWeakTable<string, TContext>();

        [TearDown]
        public void PruneContext()
        {
            _contexts.Remove(NUnitTestContext.CurrentContext.Test.ID);
        }

        public Action<TContext> ContextAmendment { get; set; }

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

                ContextAmendment?.Invoke(context);

                _contexts.Add(testContextId, context);
                return context;
            }
        }
    }
}
