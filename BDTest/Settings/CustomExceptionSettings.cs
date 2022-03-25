using System.Collections.Concurrent;

namespace BDTest.Settings;

public class CustomExceptionSettings
{
    internal CustomExceptionSettings()
    {
    }
        
    public ConcurrentBag<Type> SuccessExceptionTypes { get; } = new();
    public ConcurrentBag<Type> InconclusiveExceptionTypes { get; } = new();
}