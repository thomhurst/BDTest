namespace System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.Parameter)]
internal sealed class CallerArgumentExpressionAttribute : Attribute
{
    public CallerArgumentExpressionAttribute(string parameterName) => ParameterName = parameterName;
    public string ParameterName { get; }
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
internal sealed class OverloadResolutionPriorityAttribute : Attribute
{
    public OverloadResolutionPriorityAttribute(int priority) => Priority = priority;
    public int Priority { get; }
}
