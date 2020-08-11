using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Output;
using BDTest.Test.Steps.Given;
using BDTest.Test.Steps.When;

namespace BDTest.Test
{
    public abstract class BDTestBase
    {
        private static readonly AsyncLocal<string> AsyncLocalBDTestExecutionId = new AsyncLocal<string>();

        private static string StaticBDTestExecutionId =>
            AsyncLocalBDTestExecutionId.Value ?? (AsyncLocalBDTestExecutionId.Value = Guid.NewGuid().ToString("N"));

        protected virtual string BDTestExecutionId => StaticBDTestExecutionId;

        public Given Given(Expression<Action> step, [CallerMemberName] string callerMember = null,
            [CallerFilePath] string callerFile = null)
        {
            return new Given(new Runnable(step), callerMember, callerFile, BDTestExecutionId, this);
        }

        public Given Given(Expression<Func<Task>> step, [CallerMemberName] string callerMember = null,
            [CallerFilePath] string callerFile = null)
        {
            return new Given(new Runnable(step), callerMember, callerFile, BDTestExecutionId, this);
        }

        public When When(Expression<Action> step, [CallerMemberName] string callerMember = null,
            [CallerFilePath] string callerFile = null)
        {
            return new When(new Runnable(step), callerMember, callerFile, BDTestExecutionId, this);
        }

        public When When(Expression<Func<Task>> step, [CallerMemberName] string callerMember = null,
            [CallerFilePath] string callerFile = null)
        {
            return new When(new Runnable(step), callerMember, callerFile, BDTestExecutionId, this);
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
            TestOutputData.WriteStartupOutput(BDTestExecutionId, text);
        }

        public void WriteTearDownOutput(string text)
        {
            TestOutputData.WriteTearDownOutput(BDTestExecutionId, text);
        }

        public HtmlWriter ScenarioHtmlWriter => new HtmlWriter(BDTestExecutionId);

        public virtual Task OnRetry()
        {
            return Task.CompletedTask;
        }

        public string GetStoryText()
        {
            var storyAttribute = GetType().GetCustomAttribute(typeof(StoryAttribute)) as StoryAttribute;

            return storyAttribute?.GetStoryText();
        }

        public string GetScenarioText()
        {
            return ScenarioTextHelper.GetScenarioTextWithParameters(null).ScenarioText.Scenario;
        }
    }
}
