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
            return new Given(new Runnable(step), callerMember, callerFile, TestId);
        }

        public Given Given(Expression<Func<Task>> step, [CallerMemberName] string callerMember = null, [CallerFilePath] string callerFile = null)
        {
            return new Given(new Runnable(step), callerMember, callerFile, TestId);
        }
        
        public When When(Expression<Action> step, [CallerMemberName] string callerMember = null, [CallerFilePath] string callerFile = null)
        {
            return new When(new Runnable(step), callerMember, callerFile, TestId);
        }

        public When When(Expression<Func<Task>> step, [CallerMemberName] string callerMember = null, [CallerFilePath] string callerFile = null)
        {
            return new When(new Runnable(step), callerMember, callerFile, TestId);
        }

        public void WithContext<TContext>(Func<TContext, Scenario> test) where TContext : new()
        {
            test.Invoke(Activator.CreateInstance<TContext>());
        }

        public Task<Scenario> WithContext<TContext>(Func<TContext, Task<Scenario>> test) where TContext : new()
        {
            return test.Invoke(Activator.CreateInstance<TContext>());
        }
        
        public void WriteStartupOutput(string text)
        {
            TestOutputData.WriteStartupOutput(TestId, text);
        }

        public void WriteTearDownOutput(string text)
        {
            TestOutputData.WriteTearDownOutput(TestId, text);
        }
        
        public void WriteHtmlToReportForScenario(string html)
        {
            TestOutputData.WriteCustomHtmlForReport(TestId, html);
        }
    }
}
