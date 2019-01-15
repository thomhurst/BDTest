using System;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using BDTest.Test.Steps.Given;

namespace BDTest.Test
{
    public abstract class BDTestBase
    {
        public Given Given(Expression<Action> step, [CallerMemberName] string callerMember = null)
        {
            return new Given(step, callerMember);
        }

        public Given Given<TContext>(Func<TContext, Expression<Action>> step, [CallerMemberName] string callerMember = null) where TContext : new()
        {
            var compiledStep = step.Invoke(Activator.CreateInstance<TContext>());
            return new Given(compiledStep, callerMember);
        }
        public void WithContext<TContext>(Func<TContext, Scenario> test) where TContext : new()
        {
            test.Invoke(Activator.CreateInstance<TContext>());
        }
    }
}
