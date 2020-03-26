using System;
using System.Runtime.CompilerServices;
using BDTest.Helpers;
using BDTest.UserModels;

namespace BDTest.Test
{
    public abstract class ContextBDTestBase<TContext> : BDTestBase where TContext : class, new()
    {
        private readonly ConditionalWeakTable<string, TContext> _contexts = new ConditionalWeakTable<string, TContext>();
        private readonly TwoKeyConditionalWeakTable<string, string, object> _steps = new TwoKeyConditionalWeakTable<string, string, object>();

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

        protected TStep GetStep<TStep>() where TStep : AbstractStep<TContext>, new()
        {
            _steps.TryGetValue(TestId, typeof(TStep).FullName, out var step);
            
            step = new TStep();
            var castStep = (TStep) step;
            castStep.InitialiseContext(Context);

            _steps.Add(TestId, typeof(TStep).FullName, castStep);
            return castStep;
        }

        protected void RemoveContext()
        {
            _contexts.Remove(TestId);
            _steps.Remove(TestId);
        }
        
        public Action<TContext> ContextAmendment { get; set; }
        
        
    }
}