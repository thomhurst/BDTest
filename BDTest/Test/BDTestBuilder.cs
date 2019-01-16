using System;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BDTest.Test.Steps.Given;

namespace BDTest.Test
{
    public class BDTestBuilder
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

        public void WithContext<TContext>(Func<TContext, BDTestBuilder, Scenario> test) where TContext : new()
        {
            test.Invoke(Activator.CreateInstance<TContext>(), new BDTestBuilder());
        }

        public Task WithContext<TContext>(Func<TContext, Task<Scenario>> test) where TContext : new()
        {
            return test.Invoke(Activator.CreateInstance<TContext>());
        }
    }
}
