using BDTest.Test;
using BDTest.Output;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class ContextFactoryLifetimeTests
{
    [TestCase("async")]
    [TestCase("sync")]
    [TestCase("legacy")]
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
        Exception error = null;
        try
        {
            if (mode == "async") await fixture.Finish();
            else if (mode == "sync") fixture.FinishSync();
            else fixture.CompleteLegacy();
        }
        catch (Exception exception) { error = exception; }
        Assert.That(error?.Message, Is.EqualTo("cleanup"));
        Assert.That(TestOutputData.FrameworkExecutionId, Is.Null);
        Assert.That(TestOutputData.TestId, Is.Null);
        Assert.That(run.OutputBuffers, Is.Empty);
    }

    [TestCase("initialize")]
    [TestCase("overwrite")]
    [TestCase("overwrite async")]
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
        Assert.That(fixture.Context, Is.SameAs(context));
        Assert.That(calls, Is.EqualTo(operation == "initialize" ? 1 : 0));
        if (operation != "initialize") Assert.That(context, Is.SameAs(replacement));
        fixture.CompleteLegacy();
        Assert.That(context.Disposals, Is.EqualTo(operation == "initialize" ? 1 : 0));
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
            return new ResourceContext(events, (Dependency)creation.Services.GetService(typeof(Dependency)));
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
            Assert.That(context.Dependency, Is.SameAs(dependency));
            Assert.That(events, Is.EqualTo(new[] { "created", "configured" }));
            await fixture.Run();
            await fixture.InitializeContextAsync();
            Assert.That(events, Is.EqualTo(new[] { "created", "configured", "initialized", "step" }));
            Assert.That(creations.Single().ExecutionId, Is.EqualTo(fixture.ExecutionId));
            Assert.That(creations.Single().Attempt, Is.Zero);
            Assert.That(creations.Single().Services, Is.SameAs(provider));
        }
        finally { await fixture.Finish(); }
        Assert.That(events.Last(), Is.EqualTo("disposed"));
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
        Assert.That(context.Dependency, Is.SameAs(dependency));
        await fixture.Finish();
        Assert.That(context.Disposals, Is.EqualTo(1));
        Assert.That(dependency.Disposals, Is.Zero);
    }

    [Test]
    public async Task RetryDisposesOldContextBeforeFactoryAndInitializesEachAttempt()
    {
        var events = new List<string>();
        var attempts = new List<int>();
        var contexts = new List<ResourceContext>();
        var options = new BDTestOptions();
        options.RetryTestRules.Add(e => e is TransientException, 1);
        options.ContextFactory = new Factory(creation =>
        {
            attempts.Add(creation.Attempt);
            if (contexts.Count > 0) Assert.That(contexts.Last().Disposals, Is.EqualTo(1));
            var context = new ResourceContext(events, new Dependency());
            contexts.Add(context);
            return new ValueTask<object>(context);
        });
        using var run = BDTestRun.Start(options);
        var fixture = new Fixture(run) { FailOnce = true };
        fixture.Start();
        try
        {
            var scenario = await fixture.Run();
            Assert.That(scenario.RetryCount, Is.EqualTo(1));
            Assert.That(attempts, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(contexts.Select(c => c.Initializations), Is.EqualTo(new[] { 1, 1 }));
            Assert.That(contexts.Select(c => c.Disposals), Is.EqualTo(new[] { 1, 0 }));
        }
        finally { await fixture.Finish(); }
        await fixture.Finish();
        Assert.That(contexts.Select(c => c.Disposals), Is.EqualTo(new[] { 1, 1 }));
    }

    [Test]
    public async Task OutlineRowsDisposeAndInitializeFreshContexts()
    {
        var events = new List<string>();
        var contexts = new List<ResourceContext>();
        using var run = BDTestRun.Start(new BDTestOptions { ContextFactory = new Factory(creation =>
        {
            Assert.That(creation.Attempt, Is.Zero);
            if (contexts.Count > 0) Assert.That(contexts.Last().Disposals, Is.EqualTo(1));
            var context = new ResourceContext(events, new Dependency());
            contexts.Add(context);
            return new ValueTask<object>(context);
        }) });
        var fixture = new Fixture(run);
        fixture.Start();
        try
        {
            var outline = await fixture.ScenarioOutline("Contexts", new[] { new { Id = 1 }, new { Id = 2 } },
                _ => fixture.When(() => fixture.VerifyInitialized()).Then(() => fixture.VerifyInitialized()));
            Assert.That(outline.Examples, Has.Count.EqualTo(2));
            Assert.That(contexts.Select(c => c.Initializations), Is.EqualTo(new[] { 1, 1 }));
        }
        finally { await fixture.Finish(); }
        Assert.That(contexts.Select(c => c.Disposals), Is.EqualTo(new[] { 1, 1 }));
    }

    [TestCase("factory")]
    [TestCase("configure")]
    [TestCase("initialize")]
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
            Assert.That(await Assert.ThrowsAsync<InvalidOperationException>(async () => await fixture.Run()), Is.SameAs(expected));
            Assert.That(run.Scenarios.Single().Status, Is.EqualTo(Status.Failed));
            Assert.That(events, Does.Not.Contain("step"));
            if (contexts.Count > 0) Assert.That(contexts[0].Disposals, Is.EqualTo(1));
            await fixture.InitializeContextAsync();
            Assert.That(fixture.Context.Initializations, Is.EqualTo(1));
            Assert.That(calls, Is.EqualTo(2));
        }
        finally { await fixture.Finish(); }
        Assert.That(contexts.All(c => c.Disposals == 1), Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ThrowingDisposalDetachesContextAndAlwaysCompletesExecution(bool failedStep)
    {
        var context = new ResourceContext(new List<string>(), new Dependency())
        { DisposalError = new InvalidOperationException("dispose") };
        using var run = BDTestRun.Start(new BDTestOptions { ContextFactory = new Factory(_ => new ValueTask<object>(context)) });
        var fixture = new Fixture(run) { AlwaysFail = failedStep };
        fixture.Start();
        if (failedStep) await Assert.ThrowsAsync<TransientException>(async () => await fixture.Run());
        else await fixture.Run();
        Assert.That((await Assert.ThrowsAsync<InvalidOperationException>(async () => await fixture.Finish())).Message, Is.EqualTo("dispose"));
        await fixture.Finish();
        Assert.That(context.Disposals, Is.EqualTo(1));
        Assert.That(run.OutputBuffers, Is.Empty);
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
        var error = await Assert.ThrowsAsync<BDTest.Exceptions.ErrorOccurredDuringRetryActionException>(async () => await fixture.Run());
        Assert.That(error.InnerException, Is.SameAs(context.DisposalError));
        await fixture.Finish();
        Assert.That(context.Disposals, Is.EqualTo(1));
        Assert.That(calls, Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ReplacementDisposesOldOwnedContextAndHonorsTransferredOwnership(bool takeOwnership)
    {
        var original = new ResourceContext(new List<string>(), new Dependency());
        var replacement = new ResourceContext(new List<string>(), new Dependency());
        using var run = BDTestRun.Start(new BDTestOptions { ContextFactory = new Factory(_ => new ValueTask<object>(original)) });
        var fixture = new Fixture(run);
        fixture.Start();
        await fixture.InitializeContextAsync();
        await fixture.OverwriteContextAsync(replacement, takeOwnership);
        Assert.That(original.Disposals, Is.EqualTo(1));
        await fixture.Run();
        Assert.That(replacement.Initializations, Is.EqualTo(1));
        await fixture.Finish();
        Assert.That(replacement.Disposals, Is.EqualTo(takeOwnership ? 1 : 0));
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
        Assert.That(calls, Is.EqualTo(1));
        Assert.That(fixture.Context.Initializations, Is.EqualTo(1));
        await fixture.Finish();
    }

    private sealed class Fixture(BDTestRun run) : AbstractContextBDTestBase<ResourceContext>
    {
        public override BDTestRun TestRun => run;
        public string ExecutionId { get; } = Guid.NewGuid().ToString("N");
        public Action<BDTestContext<ResourceContext>> Configure { get; set; }
        public bool FailOnce { get; set; }
        public bool AlwaysFail { get; set; }
        private int _steps;
        protected override void ConfigureContext(BDTestContext<ResourceContext> context)
        {
            Assert.That(Context, Is.SameAs(context.TestContext));
            Configure?.Invoke(context);
            context.TestContext.Events.Add("configured");
        }
        public void Start() => BeginTest(ExecutionId);
        public ValueTask Finish() => EndTestAsync();
        public void FinishSync() => EndTest();
#pragma warning disable CS0618 // Standalone legacy callers still receive cleanup.
        public void CompleteLegacy() => MarkTestAsComplete();
#pragma warning restore CS0618
        public Task<Scenario> Run() => When(() => VerifyInitialized()).Then(() => Complete()).BDTestAsync();
        public void VerifyInitialized()
        {
            Assert.That(Context.Initializations, Is.EqualTo(1));
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
        public object GetService(Type type) => type == typeof(Dependency) ? dependency : null;
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
        public Exception InitializationError { get; set; }
        public Exception DisposalError { get; set; }
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
            Disposals++;
            Events.Add("disposed");
            if (DisposalError != null) throw DisposalError;
        }
        public void Dispose() => throw new Exception("Prefer async disposal when both interfaces exist");
    }
}
