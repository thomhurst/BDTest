using System.Collections.Concurrent;

namespace BDTest.Tests.Helpers;

// Used only by nonparallel tests: ConcurrentBag has no selective removal API.
internal sealed class ExceptionRegistration : IDisposable
{
    private readonly ConcurrentBag<Type> _types;
    private readonly Type _exceptionType;
    private bool _ownsRegistration;

    public ExceptionRegistration(ConcurrentBag<Type> types, Type exceptionType)
    {
        _types = types;
        _exceptionType = exceptionType;
        if (!types.Contains(exceptionType))
        {
            types.Add(exceptionType);
            _ownsRegistration = true;
        }
    }

    public void Dispose()
    {
        if (!_ownsRegistration)
        {
            return;
        }

        var retained = new List<Type>();
        try
        {
            while (_types.TryTake(out var type))
            {
                if (type == _exceptionType)
                {
                    break;
                }

                retained.Add(type);
            }
        }
        finally
        {
            foreach (var type in retained)
            {
                _types.Add(type);
            }

            _ownsRegistration = false;
        }
    }
}
