using System.Collections.Concurrent;

namespace BDTest.Settings;

public class CustomExceptionSettings
{
    internal CustomExceptionSettings()
    {
    }
        
    public ConcurrentBag<Type> SuccessExceptionTypes { get; } = new();

    /// <summary>Exception types that abort a scenario as skipped, without retrying it.</summary>
    public ConcurrentBag<Type> SkipExceptionTypes { get; } = new();

    public ConcurrentBag<Type> InconclusiveExceptionTypes { get; } = new();
}