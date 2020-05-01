using System;
using System.Runtime.CompilerServices;

namespace BDTest.Test
{
    public abstract class AbstractContextBDTestBase<TContext> : BDTestBase where TContext : class, new()
    {
        private readonly ConditionalWeakTable<string, TContext> _contexts = new ConditionalWeakTable<string, TContext>();

        public TContext Context
        {
            get
            {
                _contexts.TryGetValue(TestId, out var context);

                if (context != null)
                {
                    return context;
                }

                context = Activator.CreateInstance<TContext>();

                ContextAmendment?.Invoke(context);
                
                _contexts.Add(TestId, context);
                return context;
            }
        }

        public void OverwriteContext(TContext context)
        {
            if (_contexts.TryGetValue(TestId, out _))
            {
                _contexts.Remove(TestId);
            }
            
            _contexts.Add(TestId, context);
        }

        protected void RemoveContext()
        {
            _contexts.Remove(TestId);
        }
        
        public Action<TContext> ContextAmendment { get; set; }
    }
}