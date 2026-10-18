using System.Collections.Concurrent;

namespace BDTest.Settings;

public class CustomExceptionSettings
{
    private readonly object _registrationGate = new();

    /// <summary>Idempotently registers a framework declaration for the legacy default run.</summary>
    /// <remarks>Explicit runs use BDTestOptions.RegisterFrameworkOutcomes instead.</remarks>
    public void RegisterFrameworkOutcomes(FrameworkOutcomeMap map)
    {
        if (map == null) throw new ArgumentNullException(nameof(map));
        lock (_registrationGate)
        {
            map.Register(SuccessExceptionTypes.Add, SuccessExceptionTypes, SkipExceptionTypes.Add, SkipExceptionTypes,
                InconclusiveExceptionTypes.Add, InconclusiveExceptionTypes);
        }
    }

    internal CustomExceptionSettings()
    {
    }
        
    public ConcurrentBag<Type> SuccessExceptionTypes { get; } = new();

    /// <summary>Exception types that abort a scenario as skipped, without retrying it.</summary>
    public ConcurrentBag<Type> SkipExceptionTypes { get; } = new();

    public ConcurrentBag<Type> InconclusiveExceptionTypes { get; } = new();
}