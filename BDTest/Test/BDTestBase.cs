using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Metadata;
using BDTest.Output;
using BDTest.Test.Steps.Given;
using BDTest.Test.Steps.When;

namespace BDTest.Test;

public abstract class BDTestBase
{
    // Composition hosts supply the fixture that owns metadata and attributed hooks.
    internal virtual object TestInstance => this;

    /// <summary>Gets the run that owns this test's scenarios and output.</summary>
    /// <remarks>Custom adapters should override this property with a stable, explicitly owned run.</remarks>
    public virtual BDTestRun TestRun => BDTestRun.Default;

    private static readonly AsyncLocal<string?> AsyncLocalBDTestExecutionId = new();
    private readonly AsyncLocal<TestExecution?> _execution = new();
    // Some runners invoke setup and the body in separate execution contexts.
    // Their per-test fixture instance retains the identity across that boundary.
    private TestExecution? _lastExecution;
    private StartupOutputBuffer? _startupOutput;
    private static readonly TestExecution EndedExecution = new(null, null) { Completed = 1 };

    private sealed class TestExecution(string? id, BDTestRun? run, TestMethodMetadata? metadata = null)
    {
        internal string? Id { get; set; } = id;
        internal string? FrameworkId { get; } = id;
        internal BDTestRun? Run { get; } = run;
        internal TestMethodMetadata? Metadata { get; } = metadata;
        internal int Completed;
    }

    private TestExecution? CurrentExecution => _execution.Value ?? Volatile.Read(ref _lastExecution);

    internal TestMethodMetadata TestMetadata => CurrentExecution?.Metadata ?? TestMethodMetadata.Get(TestInstance.GetType(), null);

    // Retained for existing custom adapters that do not call MarkTestAsStarted.
    private static string StaticBDTestExecutionId =>
        AsyncLocalBDTestExecutionId.Value ??
        (AsyncLocalBDTestExecutionId.Value = Guid.NewGuid().ToString("N"));

    /// <summary>Gets the identity recorded by BeginTest, or a standalone flow identity.</summary>
    /// <remarks>Custom adapters should call BeginTest instead of overriding this property.</remarks>
    protected virtual string BDTestExecutionId => CurrentExecution?.FrameworkId ?? StaticBDTestExecutionId;

    /// <summary>Runs one scenario definition for each example and reports the rows as one outline.</summary>
    /// <remarks>Examples expose named public properties (for example, anonymous objects).
    /// Each row receives a fresh Context. Fixture setup and teardown run once for the whole outline.</remarks>
    public ScenarioOutlineBuilder<TExample> ScenarioOutline<TExample>(string scenarioText,
        IEnumerable<TExample> examples, Func<TExample, RunnableStepBuilder> scenario,
        [CallerMemberName] string? callerMember = null, [CallerFilePath] string? callerFile = null)
    {
        return new ScenarioOutlineBuilder<TExample>(scenarioText, examples, scenario, this,
            BDTestExecutionId, callerMember, callerFile);
    }

    internal static bool IsDefaultExecutionId(string executionId) =>
        ReferenceEquals(AsyncLocalBDTestExecutionId.Value, executionId);

    [OverloadResolutionPriority(-1)]
    public Given Given(Action step, [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new Given(new Runnable(step, argumentExpression, callerFile, callerLine), callerMember, callerFile, BDTestExecutionId, TestRun.ReportId, this);
    }

    [OverloadResolutionPriority(1)]
    public Given Given(Func<Task> step, [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new Given(new Runnable(step, argumentExpression, callerFile, callerLine), callerMember, callerFile, BDTestExecutionId, TestRun.ReportId, this);
    }

    [OverloadResolutionPriority(1)]
    public Given Given(Func<CancellationToken, Task> step, [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new Given(new Runnable(step, argumentExpression, callerFile, callerLine), callerMember, callerFile, BDTestExecutionId, TestRun.ReportId, this);
    }

    [OverloadResolutionPriority(0)]
    public Given Given(Func<ValueTask> step, [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new Given(new Runnable(step, argumentExpression, callerFile, callerLine), callerMember, callerFile, BDTestExecutionId, TestRun.ReportId, this);
    }

    [OverloadResolutionPriority(0)]
    public Given Given(Func<CancellationToken, ValueTask> step, [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new Given(new Runnable(step, argumentExpression, callerFile, callerLine), callerMember, callerFile, BDTestExecutionId, TestRun.ReportId, this);
    }

    [OverloadResolutionPriority(0)]
    public Given Given<T>(Func<ValueTask<T>> step, [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new Given(Runnable.FromResult(step, argumentExpression, callerFile, callerLine), callerMember, callerFile, BDTestExecutionId, TestRun.ReportId, this);
    }

    [OverloadResolutionPriority(0)]
    public Given Given<T>(Func<CancellationToken, ValueTask<T>> step, [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new Given(Runnable.FromResult(step, argumentExpression, callerFile, callerLine), callerMember, callerFile, BDTestExecutionId, TestRun.ReportId, this);
    }

    [OverloadResolutionPriority(-1)]
    public When When(Action step, [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new When(new Runnable(step, argumentExpression, callerFile, callerLine), callerMember, callerFile, BDTestExecutionId, TestRun.ReportId, this);
    }

    [OverloadResolutionPriority(1)]
    public When When(Func<Task> step, [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new When(new Runnable(step, argumentExpression, callerFile, callerLine), callerMember, callerFile, BDTestExecutionId, TestRun.ReportId, this);
    }

    [OverloadResolutionPriority(1)]
    public When When(Func<CancellationToken, Task> step, [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new When(new Runnable(step, argumentExpression, callerFile, callerLine), callerMember, callerFile, BDTestExecutionId, TestRun.ReportId, this);
    }

    [OverloadResolutionPriority(0)]
    public When When(Func<ValueTask> step, [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new When(new Runnable(step, argumentExpression, callerFile, callerLine), callerMember, callerFile, BDTestExecutionId, TestRun.ReportId, this);
    }

    [OverloadResolutionPriority(0)]
    public When When(Func<CancellationToken, ValueTask> step, [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new When(new Runnable(step, argumentExpression, callerFile, callerLine), callerMember, callerFile, BDTestExecutionId, TestRun.ReportId, this);
    }

    [OverloadResolutionPriority(0)]
    public When When<T>(Func<ValueTask<T>> step, [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new When(Runnable.FromResult(step, argumentExpression, callerFile, callerLine), callerMember, callerFile, BDTestExecutionId, TestRun.ReportId, this);
    }

    [OverloadResolutionPriority(0)]
    public When When<T>(Func<CancellationToken, ValueTask<T>> step, [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
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

    /// <summary>Captures files for the current step, or for this test's scenario outside a step.</summary>
    public Interfaces.IBDTestAttachments Attachments => CreateAttachmentWriter(BDTestExecutionId);

    internal Interfaces.IBDTestAttachments CreateAttachmentWriter(string executionId)
    {
        // Freeze the execution, so a saved writer cannot adopt a later test's lifetime.
        var execution = CurrentExecution;
        var run = TestRun;
        // Standalone flow IDs must remain weak so child-flow contexts can be collected.
        var ensureAttachmentsActive = IsDefaultExecutionId(executionId) ? null : run.Store.GetAttachmentExecutionGuard(executionId);
        return new AttachmentWriter(run, executionId, () =>
        {
            ensureAttachmentsActive?.Invoke();
            if (execution != null && Volatile.Read(ref execution.Completed) != 0)
                throw new InvalidOperationException("The attachment's test execution has already completed.");
        });
    }

    /// <summary>Reports a completed whole-scenario skip to the test runner.</summary>
    /// <remarks>
    /// Custom adapters can override this method to invoke their runner's skip mechanism.
    /// The standalone base leaves the skipped scenario available to the caller.
    /// </remarks>
    protected virtual void OnScenarioSkipped(string reason)
    {
    }

    /// <summary>Runs an attempt's preparation, steps and hooks inside the runner's assertion scope.</summary>
    /// <remarks>
    /// Await executeSteps once and preserve its original exception for retry classification.
    /// Use registerTerminalFailure to supply additional diagnostics that throw only if this
    /// attempt fails without retrying. Do not overwrite the runner's overall test result.
    /// </remarks>
    protected internal virtual Task ExecuteScenarioStepsAsync(Func<Task> executeSteps,
        Action<Action> registerTerminalFailure) => executeSteps();

    internal void NotifyScenarioSkipped(string reason)
    {
        var map = TestRun.Options.FrameworkOutcomes;
        if (map != null) map.ReportSkip(reason);
        else OnScenarioSkipped(reason);
    }

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

    /// <summary>Runs once per scenario attempt, after the run's before hooks and before skip checks.</summary>
    public virtual Task OnBeforeScenario(ScenarioHookContext scenario) => Task.CompletedTask;

    /// <summary>Runs after each attempt, before the run's after hooks, including skips and failures.</summary>
    /// <remarks>The scenario contains the attempt's status and exception. Existing exceptions win over cleanup failures.</remarks>
    public virtual Task OnAfterScenario(ScenarioHookContext scenario) => Task.CompletedTask;

    internal virtual ScenarioHookContext CreateScenarioHookContext(Scenario scenario, CancellationToken token) =>
        new(scenario, null, token);

    public virtual Task OnBeforeRetry()
    {
        return Task.CompletedTask;
    }

    /// <summary>Buffers constructor output before the derived fixture's run is available.</summary>
    /// <remarks>Call synchronously from the adapter constructor. The callback must start and bind the execution.</remarks>
    protected void CaptureStartupOutput(Action beginExecution)
    {
        if (beginExecution == null) throw new ArgumentNullException(nameof(beginExecution));
        _startupOutput = new StartupOutputBuffer(beginExecution);
        TestOutputData.PendingStartupOutput = _startupOutput;
        Console.SetOut(TestOutputData.Instance);
    }

    /// <summary>Binds buffered constructor output to this fixture's run and execution after BeginTest.</summary>
    protected void BindStartupOutput() => _startupOutput?.Bind(TestRun, BDTestExecutionId);

    /// <summary>Stops constructor-task capture and clears only this fixture's pending buffer. Safe to repeat.</summary>
    /// <remarks>Call before teardown completion, including when async setup fails or a legacy hook is used.</remarks>
    protected void CompleteStartupOutput()
    {
        _startupOutput?.Complete();
        if (ReferenceEquals(TestOutputData.PendingStartupOutput, _startupOutput))
            TestOutputData.PendingStartupOutput = null;
    }

    /// <summary>Invokes the pending constructor buffer's callback in the runner's current execution flow.</summary>
    /// <remarks>Use a before-test hook when derived fixtures can bypass the adapter's initialization hook.</remarks>
    protected static void BeginPendingTestExecution() => TestOutputData.PendingStartupOutput?.BeginExecution();

    /// <summary>Starts capture and context ownership for one framework test execution.</summary>
    /// <remarks>
    /// Supply an ID unique to each data row, repeat and parallel test. Call synchronously
    /// before asynchronous setup, and call EndTest at teardown. The ID remains stable
    /// across async continuations. TUnit hooks must export their AsyncLocal values.
    /// </remarks>
    protected void BeginTest(string frameworkTestId)
        => BeginTest(frameworkTestId, null);

    /// <summary>Starts an execution with the runner's exact test method, including overload and data-row identity.</summary>
    /// <remarks>Method metadata is cached. Without a method, builders use compiler-supplied caller text.</remarks>
    public void BeginTest(string frameworkTestId, MethodInfo? testMethod)
    {
        if (string.IsNullOrWhiteSpace(frameworkTestId))
            throw new ArgumentException("A test execution ID is required.", nameof(frameworkTestId));
        var run = TestRun;
        run.ThrowIfDisposed();
        // Seed the base property with this execution's ID before resolving legacy
        // overrides, including overrides that decorate base.BDTestExecutionId.
        var execution = new TestExecution(frameworkTestId, run, TestMethodMetadata.Get(TestInstance.GetType(), testMethod));
        _execution.Value = execution;
        execution.Id = BDTestExecutionId;
        run.Store.BeginAttachmentExecution(execution.Id);
        Volatile.Write(ref _lastExecution, execution);
        AsyncLocalBDTestExecutionId.Value = null;
        TestOutputData.Run = run;
        TestOutputData.TestId = null;
        TestOutputData.ConstructionOutput = null;
        TestOutputData.FrameworkExecutionId = execution.Id;
        TestOutputData.PendingStartupOutput = null;
        Console.SetOut(TestOutputData.Instance);
    }

    /// <summary>Completes this execution synchronously. Prefer EndTestAsync for asynchronous contexts.</summary>
    protected void EndTest()
    {
        var execution = CurrentExecution;
        try { ContextAsyncBridge.Run(EndTestAsync); }
        finally { ClearCompletedExecution(execution); }
    }

    /// <summary>Awaits context disposal and clears execution state even when disposal fails. Safe to repeat.</summary>
    public ValueTask EndTestAsync()
    {
        var execution = CurrentExecution;
        if (execution == null || execution == EndedExecution) return default;
        try
        {
            if (Interlocked.Exchange(ref execution.Completed, 1) != 0) return default;
            return CompleteExecutionAsync(execution);
        }
        finally
        {
            // Clear in this synchronous entry point: AsyncLocal changes inside an
            // async method do not flow back to the caller that awaits its result.
            // Disposal retains its captured flow until cleanup completes.
            ClearCompletedExecution(execution);
        }
    }

    private async ValueTask CompleteExecutionAsync(TestExecution execution)
    {
        var executionId = execution.Id ?? throw new InvalidOperationException("The execution has no ID.");
        var run = execution.Run ?? throw new InvalidOperationException("The execution has no run.");
        try { await RemoveExecutionContextAsync(executionId).ConfigureAwait(false); }
        finally
        {
            run.Store.CompleteTest(executionId);
            lock (run.Store.Gate)
            {
                if (run.Store.ScenariosByTestFrameworkId.TryGetValue(executionId, out var scenario))
                    if (scenario.Guid != null) run.OutputBuffers.Remove(scenario.Guid);
            }
        }
    }

    private void ClearCompletedExecution(TestExecution? execution)
    {
        if (execution == null || execution == EndedExecution) return;
        // A tombstone prevents repeated teardown from completing a parallel execution
        // retained in the instance fallback. Sync compatibility also clears its caller's flow.
        _execution.Value = EndedExecution;
        Interlocked.CompareExchange(ref _lastExecution, null, execution);
        if (TestOutputData.Run == execution.Run && TestOutputData.FrameworkExecutionId == execution.Id)
            ClearExecutionFlow();
    }

    internal virtual ValueTask RemoveExecutionContextAsync(string executionId) => default;
    internal virtual ValueTask InitializeExecutionContextAsync() => default;

    private static void ClearExecutionFlow()
    {
        AsyncLocalBDTestExecutionId.Value = null;
        TestOutputData.TestId = null;
        TestOutputData.FrameworkExecutionId = null;
        TestOutputData.ConstructionOutput = null;
        TestOutputData.Run = null;
        TestOutputData.PendingStartupOutput = null;
    }

    [Obsolete("Call BeginTest(frameworkTestId) instead.")]
    protected void MarkTestAsStarted()
    {
        TestRun.ThrowIfDisposed();
        // Resolve legacy overrides after rotating the standalone ID, without
        // reusing an execution from an earlier start in this flow.
        _execution.Value = EndedExecution;
        AsyncLocalBDTestExecutionId.Value = Guid.NewGuid().ToString("N");
        BeginTest(AsyncLocalBDTestExecutionId.Value);
    }

    [Obsolete("Call EndTest() instead.")]
    protected virtual void MarkTestAsComplete()
    {
        if (CurrentExecution != null)
        {
            EndTest();
            return;
        }
        // Compatibility for standalone callers and adapters that never began a test.
        var executionId = BDTestExecutionId;
        try { ContextAsyncBridge.Run(() => RemoveExecutionContextAsync(executionId)); }
        finally
        {
            TestRun.Store.CompleteTest(executionId);
            TestOutputData.Run = TestRun;
            TestOutputData.ClearCurrentTaskData();
            ClearExecutionFlow();
        }
    }

    public string? GetStoryText()
    {
        var classType = TestInstance.GetType();
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

    public string GetScenarioText([CallerMemberName] string? callerMember = null)
    {
        return ScenarioTextHelper.GetScenarioText(TestMetadata, callerMember);
    }

    internal async Task RunMethodWithAttribute<TAttribute>()
    {
        foreach (var method in GetMethodsWithAttribute<TAttribute>())
        {
            object? result;
            try
            {
                result = method.Invoke(TestInstance, Array.Empty<object>());
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
        for (var type = TestInstance.GetType(); type != null; type = type.BaseType)
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

    internal virtual ValueTask RecreateContextOnRetryAsync(int attempt) => default;
}
