using System.Reflection;

namespace BDTest.Metadata;

/// <summary>The exact selected method, including its generic construction and inherited attributes.</summary>
public sealed class StepMethodMetadata
{
    private readonly string[] _parameterSignatures;
    private readonly StepTypeReference[] _genericArguments;

    public StepMethodMetadata(StepTypeReference declaringType, string name, string documentationId,
        string[] parameterSignatures, params StepTypeReference[] genericArguments)
    {
        DeclaringType = declaringType;
        Name = name;
        DocumentationId = documentationId;
        _parameterSignatures = (string[])parameterSignatures.Clone();
        _genericArguments = (StepTypeReference[])genericArguments.Clone();
    }

    public StepTypeReference DeclaringType { get; }
    public string Name { get; }
    public string DocumentationId { get; }

    public MethodInfo Resolve(Assembly callerAssembly)
    {
        var method = DeclaringType.Resolve(callerAssembly)
            .GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                        BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Single(m => m.Name == Name && m.GetGenericArguments().Length == _genericArguments.Length &&
                         m.GetParameters().Select(p => Signature(p.ParameterType)).SequenceEqual(_parameterSignatures));
        return _genericArguments.Length == 0 ? method
            : method.MakeGenericMethod(_genericArguments.Select(t => t.Resolve(callerAssembly)).ToArray());
    }

    // This grammar is shared with the generator. Method type parameters use !!n, type parameters !n.
    private static string? Signature(Type type)
    {
        if (type.IsByRef) return Signature(type.GetElementType() ?? throw new InvalidOperationException("The type has no element type.")) + "&";
        if (type.IsArray) return Signature(type.GetElementType() ?? throw new InvalidOperationException("The type has no element type.")) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
        if (type.IsGenericParameter)
            return (type.DeclaringMethod == null ? "!" : "!!") + type.GenericParameterPosition;
        if (!type.IsGenericType) return type.FullName;
        return type.GetGenericTypeDefinition().FullName + "[" + string.Join(",", type.GetGenericArguments().Select(Signature)) + "]";
    }
}
