using System;
using System.Runtime.CompilerServices;

namespace BDTest.Test
{
    public class ContextBDTestBase<TContext> : BDTestBase where TContext : class, new()
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

                _contexts.Add(TestId, context);
                return context;
            }
        }
    }
}