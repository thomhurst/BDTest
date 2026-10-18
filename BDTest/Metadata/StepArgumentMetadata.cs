using System.Reflection;

namespace BDTest.Metadata;

/// <summary>A field or non-indexed property selected by the compiler.</summary>
public sealed class StepMemberReference
{
    public StepMemberReference(StepTypeReference declaringType, string name, bool isProperty, bool isStatic)
    {
        DeclaringType = declaringType;
        Name = name;
        IsProperty = isProperty;
        IsStatic = isStatic;
    }

    public StepTypeReference DeclaringType { get; }
    public string Name { get; }
    public bool IsProperty { get; }
    public bool IsStatic { get; }

    internal MemberInfo Resolve(Assembly assembly)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                   BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var type = DeclaringType.Resolve(assembly);
        return IsProperty ? (MemberInfo)type.GetProperty(Name, flags) : type.GetField(Name, flags);
    }
}

/// <summary>An unevaluated argument: either a compiler constant or a path of selected members.</summary>
public sealed class StepArgumentMetadata
{
    private readonly object _constant;
    private readonly StepMemberReference[] _members;

    public StepArgumentMetadata(string parameterName, object constant)
    {
        ParameterName = parameterName;
        _constant = constant;
        _members = Array.Empty<StepMemberReference>();
    }

    public StepArgumentMetadata(string parameterName, params StepMemberReference[] members)
    {
        ParameterName = parameterName;
        _members = (StepMemberReference[])members.Clone();
    }

    public string ParameterName { get; }

    internal bool TryBind(Assembly assembly, Delegate step, out Func<object> accessor)
    {
        if (_members.Length == 0)
        {
            accessor = () => _constant;
            return true;
        }

        var members = _members.Select(m => m.Resolve(assembly)).ToArray();
        // Nullable values lose their wrapper when boxed between member reads.
        if (members.Any(m => m == null) ||
            members.Skip(1).Any(m => Nullable.GetUnderlyingType(m.DeclaringType) != null) ||
            (!_members[0].IsStatic &&
            !members[0].DeclaringType.IsInstanceOfType(step.Target)))
        {
            accessor = null;
            return false;
        }

        accessor = () =>
        {
            object value = _members[0].IsStatic ? null : step.Target;
            foreach (var member in members)
            {
                value = member is FieldInfo field ? field.GetValue(value) : ((PropertyInfo)member).GetValue(value);
            }
            return value;
        };
        return true;
    }
}
