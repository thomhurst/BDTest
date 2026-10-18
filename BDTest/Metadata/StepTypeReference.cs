using System.Reflection;

namespace BDTest.Metadata;

/// <summary>A type identity that can name private types without emitting inaccessible C# references.</summary>
public sealed class StepTypeReference
{
    private readonly StepTypeReference[] _arguments;

    public StepTypeReference(string assemblyName, string metadataName, params StepTypeReference[] arguments)
    {
        AssemblyName = assemblyName;
        MetadataName = metadataName;
        _arguments = (StepTypeReference[])arguments.Clone();
    }

    public string AssemblyName { get; }
    public string MetadataName { get; }

    public Type Resolve(Assembly callerAssembly)
    {
        var assembly = callerAssembly.GetName().Name == AssemblyName
            ? callerAssembly : Assembly.Load(new AssemblyName(AssemblyName));
        var type = assembly.GetType(MetadataName, throwOnError: true);
        return _arguments.Length == 0 ? type : type.MakeGenericType(_arguments.Select(a => a.Resolve(callerAssembly)).ToArray());
    }
}
