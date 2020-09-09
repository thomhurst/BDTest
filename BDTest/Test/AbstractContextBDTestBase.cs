using System;
using System.Runtime.CompilerServices;

namespace BDTest.Test
{
    public abstract class AbstractContextBDTestBase<TContext> : BDTestBase where TContext : class, new()
    {
        private readonly ConditionalWeakTable<string, BDTestContext<TContext>> _contexts = new ConditionalWeakTable<string, BDTestContext<TContext>>();

        public TContext Context => BDTestContext.TestContext;

        public BDTestContext<TContext> BDTestContext
        {
            get
            {
                _contexts.TryGetValue(BDTestExecutionId, out var bdTestContext);

                if (bdTestContext != null)
                {
                    return bdTestContext;
                }

                var testContext = Activator.CreateInstance<TContext>();
                bdTestContext = new BDTestContext<TContext>(this, testContext, BDTestExecutionId);

                ContextAmendment?.Invoke(bdTestContext);
                
                _contexts.Add(BDTestExecutionId, bdTestContext);
                return bdTestContext;
            }
        }

        public void OverwriteContext(TContext context)
        {
            BDTestContext.TestContext = context;
        }

        protected void RemoveContext()
        {
            _contexts.Remove(BDTestExecutionId);
        }
        
        public Action<BDTestContext<TContext>> ContextAmendment { get; set; }

        internal void RecreateContextOnRetry()
        {
            OverwriteContext(Activator.CreateInstance<TContext>());   
            ContextAmendment?.Invoke(BDTestContext);
        }
    }
}