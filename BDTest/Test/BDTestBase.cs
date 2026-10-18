using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Maps;
using BDTest.Output;
using BDTest.Test.Steps.Given;
using BDTest.Test.Steps.When;

namespace BDTest.Test;

public abstract class BDTestBase
{
    private static readonly AsyncLocal<string> AsyncLocalBDTestExecutionId = new();

    // Retained for existing custom adapters that do not call MarkTestAsStarted.
    private static string StaticBDTestExecutionId
    {
        get =>
            AsyncLocalBDTestExecutionId.Value ??
            (AsyncLocalBDTestExecutionId.Value = Guid.NewGuid().ToString("N"));
        set => AsyncLocalBDTestExecutionId.Value = value;
    }

    protected virtual string BDTestExecutionId => StaticBDTestExecutionId;

    internal static bool IsDefaultExecutionId(string executionId) =>
        ReferenceEquals(AsyncLocalBDTestExecutionId.Value, executionId);

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

    /// <summary>Reports a completed whole-scenario skip to the test runner.</summary>
    /// <remarks>
    /// Custom adapters can override this method to invoke their runner's skip mechanism.
    /// The standalone base leaves the skipped scenario available to the caller.
    /// </remarks>
    protected virtual void OnScenarioSkipped(string reason)
    {
    }

    internal void NotifyScenarioSkipped(string reason) => OnScenarioSkipped(reason);

    public virtual Task OnBeforeRetry()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Starts a new execution for a custom adapter that uses the default execution ID.
    /// </summary>
    /// <remarks>
    /// Call synchronously in the execution flow that will run both setup and the test,
    /// before starting any asynchronous work. Values assigned inside an async method
    /// or Task.Run do not flow back to its caller. Call MarkTestAsComplete at teardown.
    /// Framework adapters that override BDTestExecutionId supply their own identity.
    /// </remarks>
    protected void MarkTestAsStarted()
    {
        StaticBDTestExecutionId = Guid.NewGuid().ToString("N");
        TestOutputData.TestId = null;
        TestOutputData.FrameworkExecutionId = BDTestExecutionId;
        Console.SetOut(TestOutputData.Instance);
    }

    protected virtual void MarkTestAsComplete()
    {
        TestHolder.CompleteTest(BDTestExecutionId);
        TestOutputData.ClearCurrentTaskData();
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
        foreach (var method in GetMethodsWithAttribute<TAttribute>())
        {
            object result;
            try
            {
                result = method.Invoke(this, Array.Empty<object>());
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }

            if (result is Task invokedMethodTask)
            {
                await invokedMethodTask.ConfigureAwait(false);
            }
        }
    }

    private IEnumerable<MethodInfo> GetMethodsWithAttribute<TAttribute>()
    {
        var methodsByType = new Stack<List<MethodInfo>>();
        var virtualMethods = new HashSet<MethodInfo>();

        // Walk every declaration to include private base hooks and hidden methods.
        // Select the most derived override once, then execute base classes first.
        for (var type = GetType(); type != null; type = type.BaseType)
        {
            var methods = new List<MethodInfo>();
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public |
                                                   BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                // Keep existing public static hooks; non-public discovery is for instance hooks.
                if (method.IsStatic && !method.IsPublic)
                {
                    continue;
                }

                if (method.IsVirtual && !virtualMethods.Add(method.GetBaseDefinition()))
                {
                    continue;
                }

                if (method.IsDefined(typeof(TAttribute), inherit: true))
                {
                    methods.Add(method);
                }
            }

            methodsByType.Push(methods);
        }

        return methodsByType.SelectMany(methods => methods);
    }

    internal virtual void RecreateContextOnRetry()
    {
        // No context for non-generic type
    }
}
