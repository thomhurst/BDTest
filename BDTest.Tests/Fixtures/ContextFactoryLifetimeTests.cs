using BDTest.Test;
using BDTest.Output;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

public class ContextFactoryLifetimeTests
{
    [Test]
    [Arguments("async")]
    [Arguments("sync")]
    [Arguments("legacy")]
    public async Task CompletionClearsTheCallingFlowsCaptureEvenWhenDisposalThrows(string mode)
    {
        using var run = BDTestRun.Start(new BDTestOptions { ContextFactory = new Factory(_ =>
            new ValueTask<object>(new ResourceContext(new List<string>(), new Dependency())
            { DisposalError = new InvalidOperationException("cleanup") })) });
        var fixture = new Fixture(run);
        if (mode == "legacy")
        {
            var previous = new Fixture(run);
            previous.Start();
            previous.FinishSync();
        }
        else fixture.Start();
        await fixture.Run();
        Exception? error = null;
        try
        {
            if (mode == "async") await fixture.Finish();
            else if (mode == "sync") fixture.FinishSync();
            else fixture.CompleteLegacy();
        }
        catch (Exception exception) { error = exception; }
        await Assert.That(error?.Message).IsEqualTo("cleanup");
        await Assert.That(TestOutputData.FrameworkExecutionId).IsNull();
        await Assert.That(TestOutputData.TestId).IsNull();
        await Assert.That(run.OutputBuffers).IsEmpty();
    }

    [Test]
    [Arguments("initialize")]
    [Arguments("overwrite")]
    [Arguments("overwrite async")]
    public async Task StandalonePreparationSeedsIdentityInTheCallingFlow(string operation)
    {
        var calls = 0;
        using var run = BDTestRun.Start(new BDTestOptions { ContextFactory = new Factory(_ =>
        {
            calls++;
            return new ValueTask<object>(new ResourceContext(new List<string>(), new Dependency()));
        }) });
        var previous = new Fixture(run);
        previous.Start();
        previous.FinishSync(); // Ensure the standalone flow starts without an ambient ID.
        var fixture = new Fixture(run);
        var replacement = new ResourceContext(new List<string>(), new Dependency());
        if (operation == "initialize") await fixture.InitializeContextAsync();
        else if (operation == "overwrite") fixture.OverwriteContext(replacement);
        else await fixture.OverwriteContextAsync(replacement);
        var context = fixture.Context;
        await fixture.Run();
        await Assert.That(fixture.Context).IsSameReferenceAs(context);
        await Assert.That(calls).IsEqualTo(operation == "initialize" ? 1 : 0);
        if (operation != "initialize") await Assert.That(context).IsSameReferenceAs(replacement);
        fixture.CompleteLegacy();
        await Assert.That(context.Disposals).IsEqualTo(operation == "initialize" ? 1 : 0);
        if (operation != "initialize") await replacement.DisposeAsync();
    }

    [Test]
    public async Task FactoryAndServicesAreFrozenPerRunAndInitializeBeforeSteps()
    {
        var events = new List<string>();
        var dependency = new Dependency();
        var provider = new Provider(dependency);
        var creations = new List<ContextCreation>();
        var options = new BDTestOptions { Services = provider };
        options.ContextFactory = new Factory(async creation =>
        {
            await Task.Yield();
            creations.Add(creation);
            return new ResourceContext(events, (Dependency)Required(Required(creation.Services).GetService(typeof(Dependency))));
        });
        using var run = BDTestRun.Start(options);
        options.Services = new Provider(new Dependency());
        options.ContextFactory = new Factory(_ => throw new Exception("Must use frozen factory"));
        var fixture = new Fixture(run);
        fixture.Start();
        try
        {
            // Synchronous setup access constructs/configures, but does not run async initialization.
            var context = fixture.Context;
            await Assert.That(context.Dependency).IsSameReferenceAs(dependency);
            await Assert.That(events).IsEquivalentTo(new[] { "created", "configured" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await fixture.Run();
            await fixture.InitializeContextAsync();
            await Assert.That(events).IsEquivalentTo(new[] { "created", "configured", "initialized", "step" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(creations.Single().ExecutionId).IsEqualTo(fixture.ExecutionId);
            await Assert.That(creations.Single().Attempt).IsZero();
            await Assert.That(creations.Single().Services).IsSameReferenceAs(provider);
        }
        finally { await fixture.Finish(); }
        await Assert.That(events.Last()).IsEqualTo("disposed");
    }

    [Test]
    public async Task ServiceProviderFactoryConstructsNewOwnedContextAndLeavesDependenciesOwnedByCaller()
    {
        var dependency = new Dependency();
        using var run = BDTestRun.Start(new BDTestOptions
        {
            Services = new Provider(dependency), ContextFactory = new ServiceProviderContextFactory()
        });
        var fixture = new ServiceFixture(run);
        fixture.Start();
        await fixture.InitializeContextAsync();
        var context = fixture.Context;
        await Assert.That(context.Dependency).IsSameReferenceAs(dependency);
        await fixture.Finish();
        await Assert.That(context.Disposals).IsEqualTo(1);
        await Assert.That(dependency.Disposals).IsZero();
    }

    [Test]
    public async Task RetryDisposesOldContextBeforeFactoryAndInitializesEachAttempt()
    {
        var events = new List<string>();
        var attempts = new List<int>();
        var contexts = new List<ResourceContext>();
        var options = new BDTestOptions();
        options.RetryTestRules.Add(e => e is TransientException, 1);
        options.ContextFactory = new Factory(async creation =>
        {
            attempts.Add(creation.Attempt);
            if (contexts.Count > 0) await Assert.That(contexts.Last().Disposals).IsEqualTo(1);
            var context = new ResourceContext(events, new Dependency());
            contexts.Add(context);
            return context;
        });
        using var run = BDTestRun.Start(options);
        var fixture = new Fixture(run) { FailOnce = true };
        fixture.Start();
        try
        {
            var scenario = await fixture.Run();
            await Assert.That(scenario.RetryCount).IsEqualTo(1);
            await Assert.That(attempts).IsEquivalentTo(new[] { 0, 1 }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(contexts.Select(c => c.Initializations)).IsEquivalentTo(new[] { 1, 1 }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(contexts.Select(c => c.Disposals)).IsEquivalentTo(new[] { 1, 0 }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }
        finally { await fixture.Finish(); }
        await fixture.Finish();
        await Assert.That(contexts.Select(c => c.Disposals)).IsEquivalentTo(new[] { 1, 1 }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task OutlineRowsDisposeAndInitializeFreshContexts()
    {
        var events = new List<string>();
        var contexts = new List<ResourceContext>();
        using var run = BDTestRun.Start(new BDTestOptions { ContextFactory = new Factory(async creation =>
        {
            await Assert.That(creation.Attempt).IsZero();
            if (contexts.Count > 0) await Assert.That(contexts.Last().Disposals).IsEqualTo(1);
            var context = new ResourceContext(events, new Dependency());
            contexts.Add(context);
            return context;
        }) });
        var fixture = new Fixture(run);
        fixture.Start();
        try
        {
            var outline = await fixture.ScenarioOutline("Contexts", new[] { new { Id = 1 }, new { Id = 2 } },
                _ => fixture.When(() => fixture.VerifyInitialized()).Then(() => fixture.VerifyInitialized()));
            await Assert.That(outline.Examples).Count().IsEqualTo(2);
            await Assert.That(contexts.Select(c => c.Initializations)).IsEquivalentTo(new[] { 1, 1 }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }
        finally { await fixture.Finish(); }
        await Assert.That(contexts.Select(c => c.Disposals)).IsEquivalentTo(new[] { 1, 1 }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    [Arguments("factory")]
    [Arguments("configure")]
    [Arguments("initialize")]
    public async Task FailedPreparationDetachesAndDisposesContext(string failure)
    {
        var events = new List<string>();
        var expected = new InvalidOperationException(failure);
        var contexts = new List<ResourceContext>();
        var calls = 0;
        using var run = BDTestRun.Start(new BDTestOptions { ContextFactory = new Factory(async _ =>
        {
            await Task.Yield();
            if (++calls == 1 && failure == "factory") throw expected;
            var context = new ResourceContext(events, new Dependency());
            if (calls == 1 && failure == "initialize") context.InitializationError = expected;
            contexts.Add(context);
            return context;
        }) });
        var fixture = new Fixture(run);
        fixture.Configure = _ => { if (calls == 1 && failure == "configure") throw expected; };
        fixture.Start();
        try
        {
            await Assert.That(await Assert.That(async () => await fixture.Run()).ThrowsExactly<InvalidOperationException>()).IsSameReferenceAs(expected);
            await Assert.That(run.Scenarios.Single().Status).IsEqualTo(Status.Failed);
            await Assert.That(run.Scenarios.Single().Exception?.Message).IsEqualTo(failure);
            await Assert.That(run.Scenarios.Single().Exception?.TypeName).IsEqualTo(typeof(InvalidOperationException).FullName);
            await Assert.That(run.Scenarios.Single().Exception?.ShortTypeName).IsEqualTo(nameof(InvalidOperationException));
            await Assert.That(events).DoesNotContain("step");
            if (contexts.Count > 0) await Assert.That(contexts[0].Disposals).IsEqualTo(1);
            await fixture.InitializeContextAsync();
            await Assert.That(fixture.Context.Initializations).IsEqualTo(1);
            await Assert.That(calls).IsEqualTo(2);
        }
        finally { await fixture.Finish(); }
        await Assert.That(contexts.All(c => c.Disposals == 1)).IsTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ThrowingDisposalDetachesContextAndAlwaysCompletesExecution(bool failedStep)
    {
        var context = new ResourceContext(new List<string>(), new Dependency())
        { DisposalError = new InvalidOperationException("dispose") };
        using var run = BDTestRun.Start(new BDTestOptions { ContextFactory = new Factory(_ => new ValueTask<object>(context)) });
        var fixture = new Fixture(run) { AlwaysFail = failedStep };
        fixture.Start();
        if (failedStep) await Assert.That(async () => await fixture.Run()).ThrowsExactly<TransientException>();
        else await fixture.Run();
        await Assert.That(Required(await Assert.That(async () => await fixture.Finish()).ThrowsExactly<InvalidOperationException>()).Message).IsEqualTo("dispose");
        await fixture.Finish();
        await Assert.That(context.Disposals).IsEqualTo(1);
        await Assert.That(run.OutputBuffers).IsEmpty();
    }

    [Test]
    public async Task RetryDisposalFailureStopsRetryWithoutCreatingAnotherContext()
    {
        var context = new ResourceContext(new List<string>(), new Dependency())
        { DisposalError = new InvalidOperationException("retry disposal") };
        var calls = 0;
        var options = new BDTestOptions { ContextFactory = new Factory(_ => { calls++; return new ValueTask<object>(context); }) };
        options.RetryTestRules.Add(e => e is TransientException, 1);
        using var run = BDTestRun.Start(options);
        var fixture = new Fixture(run) { AlwaysFail = true };
        fixture.Start();
        var error = await Assert.That(async () => await fixture.Run()).ThrowsExactly<BDTest.Exceptions.ErrorOccurredDuringRetryActionException>();
        await Assert.That(Required(error).InnerException).IsSameReferenceAs(context.DisposalError);
        await fixture.Finish();
        await Assert.That(context.Disposals).IsEqualTo(1);
        await Assert.That(calls).IsEqualTo(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ReplacementDisposesOldOwnedContextAndHonorsTransferredOwnership(bool takeOwnership)
    {
        var original = new ResourceContext(new List<string>(), new Dependency());
        var replacement = new ResourceContext(new List<string>(), new Dependency());
        using var run = BDTestRun.Start(new BDTestOptions { ContextFactory = new Factory(_ => new ValueTask<object>(original)) });
        var fixture = new Fixture(run);
        fixture.Start();
        await fixture.InitializeContextAsync();
        await fixture.OverwriteContextAsync(replacement, takeOwnership);
        await Assert.That(original.Disposals).IsEqualTo(1);
        await fixture.Run();
        await Assert.That(replacement.Initializations).IsEqualTo(1);
        await fixture.Finish();
        await Assert.That(replacement.Disposals).IsEqualTo(takeOwnership ? 1 : 0);
        if (!takeOwnership) await replacement.DisposeAsync();
    }

    [Test]
    public async Task ConcurrentInitializationUsesOneFactoryAndOneLifetimeCall()
    {
        var calls = 0;
        using var run = BDTestRun.Start(new BDTestOptions { ContextFactory = new Factory(async _ =>
        {
            Interlocked.Increment(ref calls);
            await Task.Yield();
            return new ResourceContext(new List<string>(), new Dependency());
        }) });
        var fixture = new Fixture(run);
        fixture.Start();
        await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(async () => await fixture.InitializeContextAsync())));
        await Assert.That(calls).IsEqualTo(1);
        await Assert.That(fixture.Context.Initializations).IsEqualTo(1);
        await fixture.Finish();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ReplacementFromConfigurationFailsWithoutDeadlock(bool asynchronous)
    {
        using var run = BDTestRun.Start(new BDTestOptions { ContextFactory = new Factory(_ =>
            new ValueTask<object>(new ResourceContext(new List<string>(), new Dependency()))) });
        var fixture = new Fixture(run);
        fixture.Start();
        var replacement = new ResourceContext(new List<string>(), new Dependency());
        fixture.Configure = _ =>
        {
            if (asynchronous) fixture.OverwriteContextAsync(replacement).GetAwaiter().GetResult();
            else fixture.OverwriteContext(replacement);
        };
        var preparation = Task.Run(async () => await fixture.InitializeContextAsync());
        try
        {
            var error = await Assert.That(async () =>
                await preparation.WaitAsync(TimeSpan.FromSeconds(5))).ThrowsExactly<InvalidOperationException>();
            await Assert.That(Required(error).Message).Contains("ConfigureContext");
        }
        finally
        {
            if (preparation.IsCompleted) await fixture.Finish();
            await replacement.DisposeAsync();
        }
    }

    [Test]
    public async Task ConcurrentInitializationWaitsForRetryDisposalAndUsesRetryAttempt()
    {
        var attempts = new List<int>();
        var contexts = new List<ResourceContext>();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var options = new BDTestOptions { ContextFactory = new Factory(creation =>
        {
            attempts.Add(creation.Attempt);
            var context = new ResourceContext(new List<string>(), new Dependency());
            if (creation.Attempt == 0)
            {
                context.DisposalStarted = started;
                context.ReleaseDisposal = release;
            }
            contexts.Add(context);
            return new ValueTask<object>(context);
        }) };
        options.RetryTestRules.Add(e => e is TransientException, 1);
        using var run = BDTestRun.Start(options);
        var fixture = new Fixture(run) { FailOnce = true };
        fixture.Start();
        var execution = fixture.Run();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var initialization = fixture.InitializeContextAsync().AsTask();
        try
        {
            await Assert.That(initialization.IsCompleted).IsFalse();
            await Assert.That(attempts).IsEquivalentTo(new[] { 0 }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("No factory may run while the old context is disposing");
        }
        finally { release.TrySetResult(); }
        try
        {
            await Task.WhenAll(execution, initialization).WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.That(attempts).IsEquivalentTo(new[] { 0, 1 }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(contexts[0].Disposals).IsEqualTo(1);
            await Assert.That(fixture.Context).IsSameReferenceAs(contexts[1]);
        }
        finally { await fixture.Finish(); }
    }

    [Test]
    [Arguments("factory")]
    [Arguments("configure")]
    [Arguments("initialize")]
    public async Task NotImplementedPreparationRetainsReportException(string failure)
    {
        var expected = new NotImplementedException(failure);
        using var run = BDTestRun.Start(new BDTestOptions { ContextFactory = new Factory(_ =>
        {
            if (failure == "factory") throw expected;
            return new ValueTask<object>(new ResourceContext(new List<string>(), new Dependency())
            { InitializationError = failure == "initialize" ? expected : null });
        }) });
        var fixture = new Fixture(run) { Configure = _ =>
        {
            if (failure == "configure") throw expected;
        } };
        fixture.Start();
        try
        {
            await Assert.That(await Assert.That(async () => await fixture.Run()).ThrowsExactly<NotImplementedException>()).IsSameReferenceAs(expected);
            var scenario = run.Scenarios.Single();
            await Assert.That(scenario.Status).IsEqualTo(Status.NotImplemented);
            await Assert.That(scenario.Exception?.Message).IsEqualTo(expected.Message);
            await Assert.That(scenario.Exception).IsNotNull();
            await Assert.That(Required(scenario.Attempts).Single().Exception?.Message).IsEqualTo(expected.Message);
            await Assert.That(Required(scenario.Steps).All(step => step.Status == Status.NotRun)).IsTrue();
        }
        finally { await fixture.Finish(); }
    }

    [Test]
    public async Task ScenarioHooksReceiveInitializedContext()
    {
        var events = new List<string>();
        var hook = new ContextHook();
        var options = new BDTestOptions { ContextFactory = new Factory(_ =>
            new ValueTask<object>(new ResourceContext(events, new Dependency()))) };
        options.ScenarioHooks.Add(hook);
        using var run = BDTestRun.Start(options);
        var fixture = new Fixture(run);
        fixture.Start();
        try
        {
            await fixture.Run();
            await Assert.That(hook.Calls).IsEquivalentTo(new[] { "before", "after" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(hook.Contexts.All(context => ReferenceEquals(context, fixture.Context))).IsTrue();
        }
        finally { await fixture.Finish(); }
    }

    [Test]
    [Arguments("factory")]
    [Arguments("configure")]
    [Arguments("initialize")]
    public async Task FailedPreparationDoesNotRunScenarioHooks(string failure)
    {
        var hook = new ContextHook();
        var options = new BDTestOptions { ContextFactory = new Factory(_ =>
        {
            if (failure == "factory") throw new InvalidOperationException(failure);
            return new ValueTask<object>(new ResourceContext(new List<string>(), new Dependency())
            { InitializationError = failure == "initialize" ? new InvalidOperationException(failure) : null });
        }) };
        options.ScenarioHooks.Add(hook);
        using var run = BDTestRun.Start(options);
        var fixture = new Fixture(run) { Configure = _ =>
        {
            if (failure == "configure") throw new InvalidOperationException(failure);
        } };
        fixture.Start();
        try
        {
            await Assert.That(async () => await fixture.Run()).ThrowsExactly<InvalidOperationException>();
            await Assert.That(hook.Calls).IsEmpty();
            await Assert.That(run.Scenarios.Single().Exception?.Message).IsEqualTo(failure);
        }
        finally { await fixture.Finish(); }
    }

    private sealed class ContextHook : BDTest.Interfaces.IScenarioHook
    {
        public List<string> Calls { get; } = new();
        public List<object> Contexts { get; } = new();
        public Task BeforeScenarioAsync(ScenarioHookContext scenario) => Record("before", scenario);
        public Task AfterScenarioAsync(ScenarioHookContext scenario) => Record("after", scenario);
        private async Task Record(string name, ScenarioHookContext scenario)
        {
            Calls.Add(name);
            Contexts.Add(Required(scenario.Context));
            await Assert.That(((ResourceContext)scenario.Context).Initializations).IsEqualTo(1);
        }
    }

    [Test]
    [Arguments("factory")]
    [Arguments("configure")]
    [Arguments("initialize")]
    public async Task SuccessfulPreparationRetryDoesNotRetainTerminalError(string failure)
    {
        var calls = 0;
        var options = new BDTestOptions { ContextFactory = new Factory(_ =>
        {
            if (++calls == 1 && failure == "factory") throw new TransientException();
            return new ValueTask<object>(new ResourceContext(new List<string>(), new Dependency())
            { InitializationError = calls == 1 && failure == "initialize" ? new TransientException() : null });
        }) };
        options.RetryTestRules.Add(error => error is TransientException, 1);
        using var run = BDTestRun.Start(options);
        var fixture = new Fixture(run) { Configure = _ =>
        {
            if (calls == 1 && failure == "configure") throw new TransientException();
        } };
        fixture.Start();
        try
        {
            var scenario = await fixture.Run();
            await Assert.That(scenario.Status).IsEqualTo(Status.Passed);
            await Assert.That(scenario.RetryCount).IsEqualTo(1);
            await Assert.That(scenario.Exception).IsNull();
            await Assert.That(scenario.Exception).IsNull();
            await Assert.That(Required(scenario.Attempts)[0].Exception).IsNotNull();
        }
        finally { await fixture.Finish(); }
    }

    [Test]
    [Arguments("teardown", false)]
    [Arguments("teardown", true)]
    [Arguments("retry", false)]
    [Arguments("retry", true)]
    [Arguments("replacement", false)]
    [Arguments("replacement", true)]
    [Arguments("preparation", false)]
    [Arguments("preparation", true)]
    public async Task CleanupReentryFailsWithoutDeadlock(string operation, bool asynchronous)
    {
        using var run = BDTestRun.Start();
        var errors = new List<Exception?>();
        var fixture = new CleanupFixture(run, asynchronous);
        fixture.Cleanup = () =>
        {
            foreach (var access in new Action[]
            {
                () => _ = fixture.Context,
                () => _ = fixture.BDTestContext,
                () => fixture.InitializeContextAsync().GetAwaiter().GetResult(),
                () => fixture.OverwriteContext(new CleanupContext(() => { })),
                fixture.Remove
            })
            {
                try { access(); errors.Add(null); }
                catch (Exception error) { errors.Add(error); }
            }
        };
        fixture.FailPreparation = operation == "preparation";
        fixture.Start();
        var execution = Task.Run(async () =>
        {
            if (operation == "preparation")
            {
                await Assert.That(async () => await fixture.InitializeContextAsync()).ThrowsExactly<InvalidOperationException>();
                return;
            }
            await fixture.InitializeContextAsync();
            if (operation == "retry") await fixture.RecreateContextOnRetryAsync(1);
            else if (operation == "replacement") await fixture.OverwriteContextAsync(new CleanupContext(() => { }));
            else await fixture.Finish();
        });
        try
        {
            await execution.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.That(errors).Count().IsEqualTo(5);
            await Assert.That(errors.All(error => error is InvalidOperationException && error.Message.Contains("cleanup"))).IsTrue();
        }
        finally
        {
            if (execution.IsCompleted) await fixture.Finish();
        }
    }

    private sealed class CleanupFixture(BDTestRun run, bool asynchronous) : AbstractContextBDTestBase<CleanupContext>
    {
        public override BDTestRun TestRun => run;
        public Action? Cleanup { get; set; }
        public bool FailPreparation { get; set; }
        protected override ValueTask<CleanupContext> CreateContextAsync(ContextCreation creation) =>
            new(asynchronous ? new AsyncCleanupContext(() => Required(Cleanup)()) : new CleanupContext(() => Required(Cleanup)()));
        protected override void ConfigureContext(BDTestContext<CleanupContext> context)
        {
            if (FailPreparation) throw new InvalidOperationException("preparation");
        }
        public void Start() => BeginTest(Guid.NewGuid().ToString("N"));
        public ValueTask Finish() => EndTestAsync();
        public void Remove() => RemoveContext();
    }

    private class CleanupContext(Action cleanup) : IDisposable
    {
        public void Dispose() => cleanup();
    }

    private sealed class AsyncCleanupContext(Action cleanup) : CleanupContext(cleanup), IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await Task.Yield();
            Dispose();
        }
    }

    private sealed class Fixture(BDTestRun run) : AbstractContextBDTestBase<ResourceContext>
    {
        public override BDTestRun TestRun => run;
        public string ExecutionId { get; } = Guid.NewGuid().ToString("N");
        public Action<BDTestContext<ResourceContext>>? Configure { get; set; }
        public bool FailOnce { get; set; }
        public bool AlwaysFail { get; set; }
        private int _steps;
        protected override void ConfigureContext(BDTestContext<ResourceContext> context)
        {
            if (!ReferenceEquals(Context, context.TestContext)) throw new AssertionException("ConfigureContext must observe the current context.");
            Configure?.Invoke(context);
            context.TestContext.Events.Add("configured");
        }
        public void Start() => BeginTest(ExecutionId);
        public ValueTask Finish() => EndTestAsync();
        public void FinishSync() => EndTest();
        // A delegate preserves the original cleanup exception without a reflection wrapper.
        public void CompleteLegacy() => BDTest.Tests.Helpers.LegacyLifecycle.Complete(this);
        public Task<BDTest.Contracts.ScenarioResult> Run() => When(() => VerifyInitialized()).Then(() => Complete()).BDTestAsync();
        public async Task VerifyInitialized()
        {
            await Assert.That(Context.Initializations).IsEqualTo(1);
            Context.Events.Add("step");
            if (AlwaysFail || FailOnce && ++_steps == 1) throw new TransientException();
        }
        private static void Complete() { }
    }

    public sealed class Dependency : IDisposable
    {
        public int Disposals { get; private set; }
        public void Dispose() => Disposals++;
    }
    public sealed class ServiceContext(Dependency dependency) : IDisposable
    {
        public Dependency Dependency { get; } = dependency;
        public int Disposals { get; private set; }
        public void Dispose() => Disposals++;
    }
    private sealed class ServiceFixture(BDTestRun run) : AbstractContextBDTestBase<ServiceContext>
    {
        public override BDTestRun TestRun => run;
        public void Start() => BeginTest(Guid.NewGuid().ToString("N"));
        public ValueTask Finish() => EndTestAsync();
    }
    private sealed class Provider(Dependency dependency) : IServiceProvider
    {
        public object? GetService(Type type) => type == typeof(Dependency) ? dependency : null;
    }
    private sealed class Factory(Func<ContextCreation, ValueTask<object>> create) : IBDTestContextFactory
    {
        public ValueTask<object> CreateContextAsync(Type contextType, ContextCreation creation) => create(creation);
    }
    private sealed class TransientException : Exception { }
    private sealed class ResourceContext : IBDTestContextLifetime, IAsyncDisposable, IDisposable
    {
        public ResourceContext(List<string> events, Dependency dependency)
        {
            Events = events;
            Dependency = dependency;
            Events.Add("created");
        }
        public List<string> Events { get; }
        public Dependency Dependency { get; }
        public int Initializations { get; private set; }
        public int Disposals { get; private set; }
        public Exception? InitializationError { get; set; }
        public Exception? DisposalError { get; set; }
        public TaskCompletionSource? DisposalStarted { get; set; }
        public TaskCompletionSource? ReleaseDisposal { get; set; }
        public async ValueTask InitializeAsync()
        {
            await Task.Yield();
            Initializations++;
            Events.Add("initialized");
            if (InitializationError != null) throw InitializationError;
        }
        public async ValueTask DisposeAsync()
        {
            await Task.Yield();
            DisposalStarted?.TrySetResult();
            if (ReleaseDisposal != null) await ReleaseDisposal.Task;
            Disposals++;
            Events.Add("disposed");
            if (DisposalError != null) throw DisposalError;
        }
        public void Dispose() => throw new Exception("Prefer async disposal when both interfaces exist");
    }
}
