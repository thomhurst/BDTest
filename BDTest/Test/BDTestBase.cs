using System;
using System.Linq.Expressions;
using BDTest.Test.Steps.Given;

namespace BDTest.Test
{
    public abstract class BDTestBase
    {
        public Given Given(Expression<Action> step)
        {
            return new Given(step);
        }

        public Given Given<TContext>(Func<TContext, Expression<Action>> step) where TContext : new()
        {
            var compiledStep = step.Invoke(Activator.CreateInstance<TContext>());
            return new Given(compiledStep);
        }
        public void WithContext<TContext>(Func<TContext, Scenario> test) where TContext : new()
        {
            test.Invoke(Activator.CreateInstance<TContext>());
        }
    }
}
