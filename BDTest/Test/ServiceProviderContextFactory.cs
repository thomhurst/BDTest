using Microsoft.Extensions.DependencyInjection;

namespace BDTest.Test;

/// <summary>Constructs contexts with dependencies from the execution's service provider.</summary>
/// <remarks>
/// Uses ActivatorUtilities.CreateInstance, so each context is newly constructed and owned by BDTest.
/// The caller owns the provider, its scope and all dependencies resolved from it.
/// </remarks>
public sealed class ServiceProviderContextFactory : IBDTestContextFactory
{
    public ValueTask<object> CreateContextAsync(Type contextType, ContextCreation creation)
    {
        if (creation.Services == null)
            throw new InvalidOperationException("Set BDTestOptions.Services when using ServiceProviderContextFactory.");
        return new ValueTask<object>(ActivatorUtilities.CreateInstance(creation.Services, contextType));
    }
}
