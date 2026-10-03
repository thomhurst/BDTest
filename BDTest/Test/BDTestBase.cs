using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Maps;
using BDTest.Output;
using BDTest.Test.Steps.Given;
using BDTest.Test.Steps.When;
using BDTest.Test.Steps.GenericStep;

namespace BDTest.Test;

public abstract class BDTestBase
{
    private static readonly AsyncLocal<string> AsyncLocalBDTestExecutionId = new();

    private static string StaticBDTestExecutionId
    {
        get =>
            AsyncLocalBDTestExecutionId.Value ??
            (AsyncLocalBDTestExecutionId.Value = Guid.NewGuid().ToString("N"));
        set => AsyncLocalBDTestExecutionId.Value = value;
    }

    protected virtual string BDTestExecutionId => StaticBDTestExecutionId;

    public Given Given(Expression<Action> step, [CallerMemberName] string callerMember = null,
        [CallerFilePath] string callerFile = null)
    {
        return new Given(new Runnable(step), callerMember, callerFile, BDTestExecutionId, TestHolder.CurrentReportId, this);
    }

    public Given Given(Expression<Func<Task>> step, [CallerMemberName] string callerMember = null,
        [CallerFilePath] string callerFile = null)
    {
        return new Given(new Runnable(step), callerMember, callerFile, BDTestExecutionId, TestHolder.CurrentReportId, this);
    }

    public When When(Expression<Action> step, [CallerMemberName] string callerMember = null,
        [CallerFilePath] string callerFile = null)
    {
        return new When(new Runnable(step), callerMember, callerFile, BDTestExecutionId, TestHolder.CurrentReportId, this);
    }

    public When When(Expression<Func<Task>> step, [CallerMemberName] string callerMember = null,
        [CallerFilePath] string callerFile = null)
    {
        return new When(new Runnable(step), callerMember, callerFile, BDTestExecutionId, TestHolder.CurrentReportId, this);
    }

    public GenericStep Step(Expression<Action> step, [CallerMemberName] string callerMember = null,
        [CallerFilePath] string callerFile = null)
    {
        return new GenericStep(new Runnable(step), callerMember, callerFile, BDTestExecutionId, TestHolder.CurrentReportId, this);
    }

    public GenericStep Step(Expression<Func<Task>> step, [CallerMemberName] string callerMember = null,
        [CallerFilePath] string callerFile = null)
    {
        return new GenericStep(new Runnable(step), callerMember, callerFile, BDTestExecutionId, TestHolder.CurrentReportId, this);
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
        TestOutputData.CollectStartupOutput(BDTestExecutionId, text, true);
    }

    public void WriteTearDownOutput(string text)
    {
        TestOutputData.CollectTearDownOutput(BDTestExecutionId, text, true);
    }

    public HtmlWriter ScenarioHtmlWriter => new(BDTestExecutionId);

    public virtual Task OnBeforeRetry()
    {
        return Task.CompletedTask;
    }

    protected virtual void MarkTestAsComplete()
    {
        AsyncLocalBDTestExecutionId.Value = null;
        TestOutputData.TestId = null;
        TestOutputData.FrameworkExecutionId = null;
    }

    public string GetStoryText()
    {
        var classType = GetType();
        var storyAttribute = classType.GetCustomAttribute(typeof(StoryAttribute)) as StoryAttribute;

        while (storyAttribute == null && classType != null)
        {
            classType = classType.DeclaringType;
            if (classType != null)
            {
                storyAttribute = classType.GetCustomAttribute(typeof(StoryAttribute)) as StoryAttribute;
            }
        }

        return storyAttribute?.GetStoryText();
    }

    public string GetScenarioText()
    {
        return ScenarioTextHelper.GetScenarioTextWithParameters(null, null).ScenarioText.Scenario;
    }

    internal async Task RunMethodWithAttribute<TAttribute>()
    {
        var methodWithAttribute = GetType().GetMethods()
            .FirstOrDefault(method =>
                method.GetCustomAttributes()
                    .Any(attribute => attribute.GetType() == typeof(TAttribute)));

        if (methodWithAttribute == null)
        {
            return;
        }

        if (methodWithAttribute.Invoke(this, Array.Empty<object>()) is Task invokedMethodTask)
        {
            await invokedMethodTask.ConfigureAwait(false);
        }
    }

    internal virtual void RecreateContextOnRetry()
    {
        // No context for non-generic type
    }
}