using System.Reflection;
using System.Threading;
using BDTest.Metadata;
using BDTest;
using BDTest.Test;

namespace BDTest.Helpers;

/// <summary>Resolves optional compiler metadata without inspecting closure fields or invoking user arguments.</summary>
internal sealed class StepTextDefinition
{
    private readonly string _argumentExpression;
    private readonly bool _omitCancellationToken;
    private readonly IReadOnlyList<Func<object?>>? _arguments;

    internal StepTextDefinition(Delegate step, string? argumentExpression, string? callerFile, int callerLine,
        bool omitCancellationToken = false, Assembly? fixtureAssembly = null)
    {
        if (step == null) throw new ArgumentNullException(nameof(step));
        _skipAttributes = new Lazy<IReadOnlyList<SkipStepAttribute>>(() =>
            Array.AsReadOnly(Method?.GetCustomAttributes(typeof(SkipStepAttribute), true)
                .Cast<SkipStepAttribute>().ToArray() ?? Array.Empty<SkipStepAttribute>()));
        _argumentExpression = argumentExpression == null || string.IsNullOrWhiteSpace(argumentExpression) ? "step" : argumentExpression;
        _omitCancellationToken = omitCancellationToken;

        var callerAssembly = step.Method.Module.Assembly;
        var metadata = StepMetadataCatalog.Find(callerAssembly, callerFile, callerLine, argumentExpression);
        if (metadata == null && fixtureAssembly != null && fixtureAssembly != callerAssembly)
        {
            callerAssembly = fixtureAssembly;
            metadata = StepMetadataCatalog.Find(callerAssembly, callerFile, callerLine, argumentExpression);
        }
        if (metadata == null)
        {
            metadata = StepMetadataCatalog.Find(callerFile, callerLine, argumentExpression, out callerAssembly);
        }

        if (metadata?.Method != null && callerAssembly != null)
        {
            Method = metadata.Method.Resolve(callerAssembly);
            if (metadata.TryBindArguments(callerAssembly, step, out var arguments))
            {
                _arguments = arguments;
            }
        }
        else if (!step.Method.Name.StartsWith("<", StringComparison.Ordinal))
        {
            // Method groups retain their method identity without a generated catalog.
            Method = step.Method;
            if (Method.GetParameters().All(parameter => parameter.ParameterType == typeof(CancellationToken)))
            {
                _arguments = Array.Empty<Func<object?>>();
            }
        }
    }

    // Resolve once per definition, on first execution so attribute constructor failures
    // receive the same exception/timing/after-hook handling as predicate failures.
    private readonly Lazy<IReadOnlyList<SkipStepAttribute>> _skipAttributes;
    internal IReadOnlyList<SkipStepAttribute> SkipAttributes => _skipAttributes.Value;

    internal MethodInfo? Method { get; }

    internal string GetText(bool evaluateArguments = false, Action<string>? diagnostic = null,
        IReadOnlyList<Func<object?>>? explicitArguments = null,
        IReadOnlyList<StepTextConverterRegistration>? converters = null, Action<StepArgument>? structuredArgument = null)
    {
        // A known method can still have unsupported captures. Preserve caller text
        // until explicit readers supply those values, while keeping Method for skip rules.
        if (Method != null && (explicitArguments != null || _arguments != null))
        {
            return StepTextHelper.GetStepText(Method, explicitArguments ?? _arguments,
                _omitCancellationToken, diagnostic, evaluateArguments, converters, structuredArgument);
        }

        if (!evaluateArguments || explicitArguments == null || explicitArguments.Count == 0)
        {
            if (Method != null) StepTextHelper.DiagnoseTemplate(Method, diagnostic);
            return _argumentExpression;
        }

        var values = explicitArguments.Select((argument, index) =>
            StepTextHelper.GetArgumentValue(argument, index.ToString(), diagnostic, converters, structuredArgument));
        return $"{_argumentExpression} {string.Join(" ", values)}".Trim();
    }
}
