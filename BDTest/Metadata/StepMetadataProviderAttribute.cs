using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace BDTest.Metadata;

/// <summary>Identifies the compiler-generated call-site catalog in a consumer assembly.</summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class StepMetadataProviderAttribute : Attribute
{
    public StepMetadataProviderAttribute(Type providerType) => ProviderType = providerType;
    public Type ProviderType { get; }
}

/// <summary>A catalog scoped to the assembly containing the step call sites.</summary>
public interface IStepMetadataProvider
{
    StepCallSiteMetadata? Find(string? callerFile, int callerLine, string? argumentExpression);
}

/// <summary>Looks up optional compiler metadata without invoking the step or its arguments.</summary>
public static class StepMetadataCatalog
{
    private static readonly ConditionalWeakTable<Assembly, Catalog> Catalogs = new();
    private static readonly object AssemblyGate = new();
    private static WeakReference<Assembly>[] _assemblies = Array.Empty<WeakReference<Assembly>>();
    private static int _assemblyVersion;
    private static int _snapshotVersion = -1;

    static StepMetadataCatalog()
    {
        AppDomain.CurrentDomain.AssemblyLoad += (_, _) => Interlocked.Increment(ref _assemblyVersion);
    }

    /// <summary>Finds a unique loaded call-site catalog, including tests inherited from another assembly.</summary>
    /// <remarks>Ambiguous identities return null. Catalogs are cached without preventing assembly unloading.</remarks>
    public static StepCallSiteMetadata? Find(string? callerFile, int callerLine, string? argumentExpression,
        out Assembly? callerAssembly)
    {
        callerAssembly = null;
        StepCallSiteMetadata? result = null;
        foreach (var reference in LoadedAssemblies())
        {
            if (!reference.TryGetTarget(out var assembly)) continue;
            var candidate = Find(assembly, callerFile, callerLine, argumentExpression);
            if (candidate == null) continue;
            if (result != null)
            {
                callerAssembly = null;
                return null;
            }
            result = candidate;
            callerAssembly = assembly;
        }
        return result;
    }

    public static StepCallSiteMetadata? Find(Assembly callerAssembly, string? callerFile, int callerLine,
        string? argumentExpression)
    {
        if (callerAssembly == null) throw new ArgumentNullException(nameof(callerAssembly));
        return Catalogs.GetValue(callerAssembly, CreateCatalog).Provider?.Find(callerFile, callerLine, argumentExpression);
    }

    private static Catalog CreateCatalog(Assembly assembly)
    {
        var attribute = assembly.GetCustomAttribute<StepMetadataProviderAttribute>();
        return new Catalog(attribute == null ? null : (IStepMetadataProvider?)Activator.CreateInstance(attribute.ProviderType));
    }

    private static WeakReference<Assembly>[] LoadedAssemblies()
    {
        lock (AssemblyGate)
        {
            var version = Volatile.Read(ref _assemblyVersion);
            if (_snapshotVersion != version)
            {
                _assemblies = AppDomain.CurrentDomain.GetAssemblies().Select(a => new WeakReference<Assembly>(a)).ToArray();
                _snapshotVersion = version;
            }
            return _assemblies;
        }
    }

    private sealed class Catalog
    {
        public Catalog(IStepMetadataProvider? provider) => Provider = provider;
        public IStepMetadataProvider? Provider { get; }
    }
}
