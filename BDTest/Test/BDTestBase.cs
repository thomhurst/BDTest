using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Output;
using BDTest.Test.Steps.Given;
using BDTest.Test.Steps.When;

namespace BDTest.Test;

public abstract class BDTestBase
{
    /// <summary>Gets the run that owns this test's scenarios and output.</summary>
    /// <remarks>Custom adapters should override this property with a stable, explicitly owned run.</remarks>
    public virtual BDTestRun TestRun => BDTestRun.Default;

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

    /// <summary>Runs one scenario definition for each example and reports the rows as one outline.</summary>
    /// <remarks>Examples expose named public properties (for example, anonymous objects).
    /// Each row receives a fresh Context. Fixture setup and teardown run once for the whole outline.</remarks>
    public ScenarioOutlineBuilder<TExample> ScenarioOutline<TExample>(string scenarioText,
        IEnumerable<TExample> examples, Func<TExample, RunnableStepBuilder> scenario,
        [CallerMemberName] string callerMember = null, [CallerFilePath] string callerFile = null)
    {
        return new ScenarioOutlineBuilder<TExample>(scenarioText, examples, scenario, this,
            BDTestExecutionId, callerMember, callerFile);
    }

    internal static bool IsDefaultExecutionId(string executionId) =>
        ReferenceEquals(AsyncLocalBDTestExecutionId.Value, executionId);

    [OverloadResolutionPriority(-1)]
    public Given Given(Action step, [CallerMemberName] string callerMember = null,
        [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new Given(new Runnable(step, argumentExpression, callerFile, callerLine), callerMember, callerFile, BDTestExecutionId, TestRun.ReportId, this);
    }

    [OverloadResolutionPriority(1)]
    public Given Given(Func<Task> step, [CallerMemberName] string callerMember = null,
        [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new Given(new Runnable(step, argumentExpression, callerFile, callerLine), callerMember, callerFile, BDTestExecutionId, TestRun.ReportId, this);
    }

    [OverloadResolutionPriority(1)]
    public Given Given(Func<CancellationToken, Task> step, [CallerMemberName] string callerMember = null,
        [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new Given(new Runnable(step, argumentExpression, callerFile, callerLine), callerMember, callerFile, BDTestExecutionId, TestRun.ReportId, this);
    }

    [OverloadResolutionPriority(0)]
    public Given Given(Func<ValueTask> step, [CallerMemberName] string callerMember = null,
        [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new Given(new Runnable(step, argumentExpression, callerFile, callerLine), callerMember, callerFile, BDTestExecutionId, TestRun.ReportId, this);
    }

    [OverloadResolutionPriority(0)]
    public Given Given(Func<CancellationToken, ValueTask> step, [CallerMemberName] string callerMember = null,
        [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new Given(new Runnable(step, argumentExpression, callerFile, callerLine), callerMember, callerFile, BDTestExecutionId, TestRun.ReportId, this);
    }

    [OverloadResolutionPriority(0)]
    public Given Given<T>(Func<ValueTask<T>> step, [CallerMemberName] string callerMember = null,
        [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new Given(Runnable.FromResult(step, argumentExpression, callerFile, callerLine), callerMember, callerFile, BDTestExecutionId, TestRun.ReportId, this);
    }

    [OverloadResolutionPriority(0)]
    public Given Given<T>(Func<CancellationToken, ValueTask<T>> step, [CallerMemberName] string callerMember = null,
        [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new Given(Runnable.FromResult(step, argumentExpression, callerFile, callerLine), callerMember, callerFile, BDTestExecutionId, TestRun.ReportId, this);
    }

    [OverloadResolutionPriority(-1)]
    public When When(Action step, [CallerMemberName] string callerMember = null,
        [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new When(new Runnable(step, argumentExpression, callerFile, callerLine), callerMember, callerFile, BDTestExecutionId, TestRun.ReportId, this);
    }

    [OverloadResolutionPriority(1)]
    public When When(Func<Task> step, [CallerMemberName] string callerMember = null,
        [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new When(new Runnable(step, argumentExpression, callerFile, callerLine), callerMember, callerFile, BDTestExecutionId, TestRun.ReportId, this);
    }

    [OverloadResolutionPriority(1)]
    public When When(Func<CancellationToken, Task> step, [CallerMemberName] string callerMember = null,
        [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new When(new Runnable(step, argumentExpression, callerFile, callerLine), callerMember, callerFile, BDTestExecutionId, TestRun.ReportId, this);
    }

    [OverloadResolutionPriority(0)]
    public When When(Func<ValueTask> step, [CallerMemberName] string callerMember = null,
        [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new When(new Runnable(step, argumentExpression, callerFile, callerLine), callerMember, callerFile, BDTestExecutionId, TestRun.ReportId, this);
    }

    [OverloadResolutionPriority(0)]
    public When When(Func<CancellationToken, ValueTask> step, [CallerMemberName] string callerMember = null,
        [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new When(new Runnable(step, argumentExpression, callerFile, callerLine), callerMember, callerFile, BDTestExecutionId, TestRun.ReportId, this);
    }

    [OverloadResolutionPriority(0)]
    public When When<T>(Func<ValueTask<T>> step, [CallerMemberName] string callerMember = null,
        [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new When(Runnable.FromResult(step, argumentExpression, callerFile, callerLine), callerMember, callerFile, BDTestExecutionId, TestRun.ReportId, this);
    }

    [OverloadResolutionPriority(0)]
    public When When<T>(Func<CancellationToken, ValueTask<T>> step, [CallerMemberName] string callerMember = null,
        [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new When(Runnable.FromResult(step, argumentExpression, callerFile, callerLine), callerMember, callerFile, BDTestExecutionId, TestRun.ReportId, this);
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
        TestOutputData.CollectStartupOutput(BDTestExecutionId, text, true, run: TestRun);
    }

    public void WriteTearDownOutput(string text)
    {
        TestOutputData.CollectTearDownOutput(BDTestExecutionId, text, true, TestRun);
    }

    public HtmlWriter ScenarioHtmlWriter => new(BDTestExecutionId, TestRun);

    /// <summary>Reports a completed whole-scenario skip to the test runner.</summary>
    /// <remarks>
    /// Custom adapters can override this method to invoke their runner's skip mechanism.
    /// The standalone base leaves the skipped scenario available to the caller.
    /// </remarks>
    protected virtual void OnScenarioSkipped(string reason)
    {
    }

    internal void NotifyScenarioSkipped(string reason) => OnScenarioSkipped(reason);

    /// <summary>Runs before each reached step, after the global before hooks.</summary>
    public virtual Task OnBeforeStep(Step step) => Task.CompletedTask;

    /// <summary>Runs after each reached step, before the global after hooks.</summary>
    /// <remarks>
    /// The step contains its status, exception, output and timing. This hook runs even when the
    /// step or a before hook throws, and for individually skipped steps, but not for unreached steps
    /// or a skipped scenario. Hooks run again on each retry. An existing exception takes precedence
    /// over an after-hook exception; otherwise a hook exception determines the step result.
    /// </remarks>
    public virtual Task OnAfterStep(Step step) => Task.CompletedTask;

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
        TestRun.ThrowIfDisposed();
        TestOutputData.Run = TestRun;
        StaticBDTestExecutionId = Guid.NewGuid().ToString("N");
        TestOutputData.TestId = null;
        TestOutputData.FrameworkExecutionId = BDTestExecutionId;
        Console.SetOut(TestOutputData.Instance);
    }

    protected virtual void MarkTestAsComplete()
    {
        TestRun.Store.CompleteTest(BDTestExecutionId);
        TestOutputData.Run = TestRun;
        TestOutputData.ClearCurrentTaskData();
        AsyncLocalBDTestExecutionId.Value = null;
        TestOutputData.TestId = null;
        TestOutputData.FrameworkExecutionId = null;
        TestOutputData.Run = null;
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
                await invokedMethodTask;
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
