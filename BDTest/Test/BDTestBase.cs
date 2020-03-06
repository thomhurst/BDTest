using System;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using BDTest.Output;
using BDTest.Test.Steps.Given;
using BDTest.Test.Steps.When;

namespace BDTest.Test
{
    public abstract class BDTestBase
    {
        private static readonly AsyncLocal<string> AsyncLocalTestId = new AsyncLocal<string>();
        private static string InternalTestId => AsyncLocalTestId.Value ?? (AsyncLocalTestId.Value = Guid.NewGuid().ToString("N"));

        protected virtual string TestId => InternalTestId;
        
        public Given Given(Expression<Action> step, [CallerMemberName] string callerMember = null, [CallerFilePath] string callerFile = null)
        {
            return new Given(step, callerMember, callerFile, TestId);
        }

        public Given Given(Expression<Func<Task>> step, [CallerMemberName] string callerMember = null, [CallerFilePath] string callerFile = null)
        {
            return new Given(step, callerMember, callerFile, TestId);
        }
        
        public When When(Expression<Action> step, [CallerMemberName] string callerMember = null, [CallerFilePath] string callerFile = null)
        {
            return new When(step, callerMember, callerFile, TestId);
        }

        public When When(Expression<Func<Task>> step, [CallerMemberName] string callerMember = null, [CallerFilePath] string callerFile = null)
        {
            return new When(step, callerMember, callerFile, TestId);
        }

        public void WithContext<TContext>(Func<TContext, Scenario> test) where TContext : new()
        {
            test.Invoke(Activator.CreateInstance<TContext>());
        }

        public void WithContext<TContext, TContext2>(Func<TContext, TContext2, Scenario> test) where TContext : new() where TContext2 : new()
        {
            test.Invoke(Activator.CreateInstance<TContext>(), Activator.CreateInstance<TContext2>());
        }

        public void WithContext<TContext, TContext2, TContext3>(Func<TContext, TContext2, TContext3, Scenario> test) where TContext : new() where TContext2 : new() where TContext3 : new()
        {
            test.Invoke(Activator.CreateInstance<TContext>(), Activator.CreateInstance<TContext2>(), Activator.CreateInstance<TContext3>());
        }

        public void WithContext<TContext, TContext2, TContext3, TContext4>(Func<TContext, TContext2, TContext3, TContext4, Scenario> test) where TContext : new() where TContext2 : new() where TContext3 : new() where TContext4 : new()
        {
            test.Invoke(Activator.CreateInstance<TContext>(), Activator.CreateInstance<TContext2>(), Activator.CreateInstance<TContext3>(), Activator.CreateInstance<TContext4>());
        }

        public void WithContext<TContext, TContext2, TContext3, TContext4, TContext5>(Func<TContext, TContext2, TContext3, TContext4, TContext5, Scenario> test) where TContext : new() where TContext2 : new() where TContext3 : new() where TContext4 : new() where TContext5 : new()
        {
            test.Invoke(Activator.CreateInstance<TContext>(), Activator.CreateInstance<TContext2>(), Activator.CreateInstance<TContext3>(), Activator.CreateInstance<TContext4>(), Activator.CreateInstance<TContext5>());
        }

        public Task<Scenario> WithContext<TContext>(Func<TContext, Task<Scenario>> test) where TContext : new()
        {
            return test.Invoke(Activator.CreateInstance<TContext>());
        }

        public Task<Scenario> WithContext<TContext, TContext2>(Func<TContext, TContext2, Task<Scenario>> test) where TContext : new() where TContext2 : new()
        {
            return test.Invoke(Activator.CreateInstance<TContext>(), Activator.CreateInstance<TContext2>());
        }

        public Task<Scenario> WithContext<TContext, TContext2, TContext3>(Func<TContext, TContext2, TContext3, Task<Scenario>> test) where TContext : new() where TContext2 : new() where TContext3 : new()
        {
            return test.Invoke(Activator.CreateInstance<TContext>(), Activator.CreateInstance<TContext2>(), Activator.CreateInstance<TContext3>());
        }

        public Task<Scenario> WithContext<TContext, TContext2, TContext3, TContext4>(Func<TContext, TContext2, TContext3, TContext4, Task<Scenario>> test) where TContext : new() where TContext2 : new() where TContext3 : new() where TContext4 : new()
        {
            return test.Invoke(Activator.CreateInstance<TContext>(), Activator.CreateInstance<TContext2>(), Activator.CreateInstance<TContext3>(), Activator.CreateInstance<TContext4>());
        }

        public Task<Scenario> WithContext<TContext, TContext2, TContext3, TContext4, TContext5>(Func<TContext, TContext2, TContext3, TContext4, TContext5, Task<Scenario>> test) where TContext : new() where TContext2 : new() where TContext3 : new() where TContext4 : new() where TContext5 : new()
        {
            return test.Invoke(Activator.CreateInstance<TContext>(), Activator.CreateInstance<TContext2>(), Activator.CreateInstance<TContext3>(), Activator.CreateInstance<TContext4>(), Activator.CreateInstance<TContext5>());
        }

        public async Task WriteTearDownOutput(string text)
        {
            await TestOutputData.WriteTearDownOutput(TestId, text);
        }
    }
}
