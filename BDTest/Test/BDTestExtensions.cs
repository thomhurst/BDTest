using System;
using System.Linq.Expressions;
using BDTest.Test.Steps.Given;

namespace BDTest.Test
{
    public static class BDTestExtensions
    {
        public static Given Given(this object testClass, Expression<Action> step)
        {
            return new Given(step);
        }

        public static Given Given<TContext>(this object testClass, Func<TContext, Expression<Action>> step) where TContext : new()
        {
            var compiledStep = step.Invoke(Activator.CreateInstance<TContext>());
            return new Given(compiledStep);
        }
        public static void WithContext<TContext>(this object testClass, Func<TContext, Scenario> test) where TContext : new()
        {
            test.Invoke(Activator.CreateInstance<TContext>());
        }
    }
}
