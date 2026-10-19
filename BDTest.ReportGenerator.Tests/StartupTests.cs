using BDTest.Reports.Storage.AzureBlob;
using BDTest.Razor.Reports.Storage;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TUnit.Assertions.Enums;
using BDTest.Contracts;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;
using BDTest.ReportGenerator.RazorServer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BDTest.ReportGenerator.Tests;

public class StartupTests
{
    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    [Arguments("invalid-connection-string")]
    public async Task Invalid_storage_configuration_fails_startup_with_the_setting_name(string? connectionString)
    {
        using var host = CreateHost(new RecordingStore(), connectionString);
        var exception = await Assert.ThrowsExactlyAsync<OptionsValidationException>(() => host.StartAsync());
        await Assert.That(exception!.Message).Contains("AzureStorage:ConnectionString");
    }

    [Test]
    public async Task Startup_awaits_one_cache_load_and_populates_the_repository()
    {
        var store = new RecordingStore();
        using var host = CreateHost(store);
        var starting = host.StartAsync();
        try
        {
            await store.LoadStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(starting.IsCompleted).IsFalse();
        }
        finally
        {
            store.ReleaseLoad.TrySetResult();
            await starting;
        }

        var records = await host.Services.GetRequiredService<IDataRepository>().GetAllTestRunRecords();
        await Assert.That(records.Select(record => record.RecordId))
            .IsEquivalentTo(new[] { "startup-report" }.Cast<string?>(), CollectionOrdering.Matching);
        await Assert.That(store.Loads).IsEqualTo(1);
        await host.StopAsync();
    }

    [Test]
    public async Task Storage_load_failure_is_propagated_from_startup()
    {
        var failure = new InvalidOperationException("storage unavailable");
        var store = new RecordingStore { Failure = failure };
        store.ReleaseLoad.SetResult();
        var logger = new RecordingLogger();
        using var host = CreateHost(store, logger: logger);
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => host.StartAsync());
        await Assert.That(exception).IsSameReferenceAs(failure);
        await Assert.That(logger.Errors).Contains(failure);
    }

    [Test]
    public async Task Initialization_failure_is_logged_and_prevents_cache_loading()
    {
        var failure = new InvalidOperationException("initialization failed");
        var store = new RecordingStore { InitializationFailure = failure };
        var logger = new RecordingLogger();
        using var host = CreateHost(store, logger: logger);
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => host.StartAsync());
        await Assert.That(exception).IsSameReferenceAs(failure);
        await Assert.That(logger.Errors).Contains(failure);
        await Assert.That(store.Loads).IsZero();
    }

    [Test]
    public async Task Startup_cancellation_reaches_initialization_and_prevents_cache_loading()
    {
        var store = new RecordingStore { WaitForCancellation = true };
        using var cancellation = new CancellationTokenSource();
        using var host = CreateHost(store);
        var starting = host.StartAsync(cancellation.Token);
        try
        {
            await store.InitializationStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(starting.IsCompleted).IsFalse();
        }
        finally
        {
            cancellation.Cancel();
        }
        await Assert.ThrowsAsync<OperationCanceledException>(() => starting);
        await Assert.That(store.Loads).IsZero();
    }

    private static IHost CreateHost(RecordingStore store, string? connectionString = "UseDevelopmentStorage=true", RecordingLogger? logger = null)
    {
        var builder = Program.CreateBuilder(["--environment", Environments.Development]);
        builder.WebHost.UseTestServer();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AzureAd:Instance"] = "https://login.microsoftonline.com/",
            ["AzureAd:TenantId"] = "11111111-1111-1111-1111-111111111111",
            ["AzureAd:ClientId"] = "22222222-2222-2222-2222-222222222222",
            ["AzureStorage:ConnectionString"] = connectionString
        });
        builder.Services.RemoveAll<IReportStore>();
        builder.Services.RemoveAll<IRunIndexStore>();
        builder.Services.AddSingleton<IBDTestDataStore>(store);
        if (logger != null) builder.Services.AddLogging(logging => logging.AddProvider(logger));
        return Program.BuildApplication(builder);
    }

    private sealed class RecordingStore : IBDTestDataStore, IAsyncDataStoreInitializer
    {
        public int Loads;
        public Exception? InitializationFailure { get; init; }
        public bool WaitForCancellation { get; init; }
        private bool _initialized;
        public TaskCompletionSource InitializationStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            InitializationStarted.TrySetResult();
            if (WaitForCancellation) await Task.Delay(Timeout.Infinite, cancellationToken);
            if (InitializationFailure != null) throw InitializationFailure;
            _initialized = true;
        }

        public Exception? Failure { get; init; }
        public TaskCompletionSource LoadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseLoad { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<IEnumerable<TestRunSummary>> GetAllTestRunRecords()
        {
            await Assert.That(_initialized).IsTrue().Because("Storage must be initialized before warm-up");
            Interlocked.Increment(ref Loads);
            LoadStarted.TrySetResult();
            await ReleaseLoad.Task;
            if (Failure != null) throw Failure;
            return new[] { ReportJson.DeserializeObject<TestRunSummary>("{\"RecordId\":\"startup-report\"}") };
        }

        public Task<BDTestReport?> GetTestData(string id, CancellationToken cancellationToken) => Task.FromResult<BDTestReport?>(null);
        public Task StoreTestData(string id, BDTestReport? data) => Task.CompletedTask;
        public Task DeleteTestData(string id) => Task.CompletedTask;
        public Task StoreTestRunRecord(TestRunSummary summary) => Task.CompletedTask;
        public Task DeleteTestRunRecord(string id) => Task.CompletedTask;
    }

    private sealed class RecordingLogger : ILoggerProvider, ILogger
    {
        public List<Exception> Errors { get; } = new();
        public ILogger CreateLogger(string categoryName) => this;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error && exception != null) Errors.Add(exception);
        }
        public void Dispose() { }
    }

}
