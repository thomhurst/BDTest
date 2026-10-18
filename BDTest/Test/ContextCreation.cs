namespace BDTest.Test;

/// <summary>Identifies the test and zero-based attempt requesting a context.</summary>
public sealed class ContextCreation
{
    internal ContextCreation(string executionId, int attempt, IServiceProvider services)
    {
        ExecutionId = executionId;
        Attempt = attempt;
        Services = services;
    }

    public string ExecutionId { get; }
    public int Attempt { get; }
    public IServiceProvider Services { get; }
}

/// <summary>Optional asynchronous initialization, awaited once before each attempt's first step.</summary>
public interface IBDTestContextLifetime
{
    ValueTask InitializeAsync();
}

/// <summary>Creates a new, framework-owned context for each execution and attempt.</summary>
public interface IBDTestContextFactory
{
    ValueTask<object> CreateContextAsync(Type contextType, ContextCreation creation);
}
