using System.Reflection;

namespace BDTest.Metadata;

/// <summary>Optional metadata keyed by the compiler's caller file, line and lambda source text.</summary>
public sealed class StepCallSiteMetadata
{
    private readonly StepArgumentMetadata[] _arguments;

    public StepCallSiteMetadata(string? callerFile, int callerLine, string? argumentExpression,
        StepMethodMetadata? method, bool supportsArgumentCapture, params StepArgumentMetadata[] arguments)
    {
        CallerFile = callerFile;
        CallerLine = callerLine;
        ArgumentExpression = argumentExpression;
        Method = method;
        SupportsArgumentCapture = supportsArgumentCapture;
        _arguments = (StepArgumentMetadata[])arguments.Clone();
    }

    public string? CallerFile { get; }
    public int CallerLine { get; }
    public string? ArgumentExpression { get; }
    /// <summary>Null for unsupported bodies. Preserve ArgumentExpression as the fallback.</summary>
    public StepMethodMetadata? Method { get; }
    public bool SupportsArgumentCapture { get; }
    public IReadOnlyList<string> ParameterNames => Array.AsReadOnly(_arguments.Select(a => a.ParameterName).ToArray());

    /// <summary>Binds lazy accessors. Does not run getters, instantiate attributes, or invoke the step.</summary>
    /// <remarks>False means use caller text and explicit arguments. Never inspect compiler-generated closure fields.</remarks>
    public bool TryBindArguments(Assembly callerAssembly, Delegate step, out IReadOnlyList<Func<object?>> accessors)
    {
        if (step == null) throw new ArgumentNullException(nameof(step));
        var result = new List<Func<object?>>();
        if (Method != null && SupportsArgumentCapture)
        {
            foreach (var argument in _arguments)
            {
                if (!argument.TryBind(callerAssembly, step, out var accessor))
                {
                    accessors = Array.Empty<Func<object?>>();
                    return false;
                }
                result.Add(accessor);
            }
            accessors = result.AsReadOnly();
            return true;
        }
        accessors = Array.Empty<Func<object?>>();
        return false;
    }
}
