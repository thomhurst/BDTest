using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading;
using BDTest.Compiler;
using BDTest.Test;

namespace BDTest.Helpers;

internal static class StepTextHelper
{
    /// <summary>Renders a selected step method using explicit, lazy argument readers.</summary>
    /// <remarks>Supply a run to use its frozen converters. Without a run, only default value formatting applies.</remarks>
    public static string GetStepText(MethodInfo method, IReadOnlyList<Func<object?>>? arguments,
        bool omitCancellationToken = false, Action<string>? diagnostic = null, BDTestRun? run = null)
    {
        if (method == null) throw new ArgumentNullException(nameof(method));
        run?.ThrowIfDisposed();
        return GetStepText(method, arguments, omitCancellationToken, diagnostic, evaluateArguments: true, converters: run?.Options.Converters);
    }

    internal static void DiagnoseTemplate(MethodInfo method, Action<string>? diagnostic)
    {
        if (diagnostic == null) return;
        var attribute = method.GetCustomAttribute<StepTextAttribute>(inherit: true);
        DiagnoseTemplate(method, attribute, diagnostic);
    }

    private static void DiagnoseTemplate(MethodInfo method, StepTextAttribute? attribute, Action<string>? diagnostic)
    {
        if (diagnostic == null) return;
        if (attribute?.Text == null) return;
        foreach (var message in NamedStepTemplate.Validate(attribute.Text,
                     method.GetParameters().Where(p => p.ParameterType != typeof(CancellationToken)).Select(p => p.Name ?? ""),
                     attribute.HasNullOverrideArray ? null : attribute.ParameterOverrides))
            diagnostic(message);
    }

    internal static string GetStepText(MethodInfo method, IReadOnlyList<Func<object?>>? arguments,
        bool omitCancellationToken, Action<string>? diagnostic, bool evaluateArguments,
        IReadOnlyList<StepTextConverterRegistration>? converters = null, Action<StepArgument>? structuredArgument = null)
    {
        var parameters = method.GetParameters();
        // Tokens never contribute report values, including parameterless delegates that pass one explicitly.
        var visibleParameters = parameters.Where(p => p.ParameterType != typeof(CancellationToken)).ToArray();
        if (evaluateArguments && arguments != null && arguments.Count != parameters.Length && arguments.Count != visibleParameters.Length)
        {
            throw new ArgumentException($"Expected {visibleParameters.Length} step text argument readers, but received {arguments.Count}.", nameof(arguments));
        }

        var attribute = method.GetCustomAttribute<StepTextAttribute>(inherit: true);
        DiagnoseTemplate(method, attribute, diagnostic);
        var orderedValues = visibleParameters.Select((parameter, index) =>
        {
            var argumentIndex = arguments?.Count == parameters.Length ? parameter.Position : index;
            return evaluateArguments && arguments != null
                ? ReadArgument(arguments[argumentIndex], parameter.Name, diagnostic, converters, structuredArgument)
                : new ArgumentValue(null, null, parameter.Name, diagnostic);
        }).ToArray();
        var values = visibleParameters.Select((parameter, index) => (Name: parameter.Name ?? "", Value: orderedValues[index]))
            .ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal);

        if (attribute?.Text == null)
        {
            var name = NameHumanizer.Humanize(method.Name);
            if (!string.IsNullOrWhiteSpace(name) && char.IsUpper(name[0]))
                name = char.ToLowerInvariant(name[0]) + name.Substring(1);
            return $"{name} {string.Join(" ", orderedValues.Select(value => value.Format(null)))}".Trim();
        }

        var overrides = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var replacement in attribute.ParameterOverrides)
            if (NamedStepTemplate.TryParseOverride(replacement, out var name, out var text) && values.ContainsKey(name))
                overrides[name] = text;

        var tokens = new HashSet<string>(parameters.Where(p => p.ParameterType == typeof(CancellationToken)).Select(p => p.Name ?? ""), StringComparer.Ordinal);
        var result = new StringBuilder();
        foreach (var token in NamedStepTemplate.Parse(attribute.Text))
        {
            if (token.Name == null || token.IsPositional) result.Append(token.Text);
            else if (tokens.Contains(token.Name)) { /* Do not render token placeholders or overrides. */ }
            else if (overrides.TryGetValue(token.Name, out var replacement)) result.Append(replacement);
            else if (values.TryGetValue(token.Name, out var value)) result.Append(value.Format(token.Format));
            else result.Append(token.Text);
        }
        return result.ToString();
    }

    internal static string GetArgumentValue(Func<object?> argument, string? name, Action<string>? diagnostic,
        IReadOnlyList<StepTextConverterRegistration>? converters = null, Action<StepArgument>? structuredArgument = null) =>
        ReadArgument(argument, name, diagnostic, converters, structuredArgument).Format(null);

    private static ArgumentValue ReadArgument(Func<object?> argument, string? name, Action<string>? diagnostic,
        IReadOnlyList<StepTextConverterRegistration>? converters, Action<StepArgument>? structuredArgument)
    {
        try
        {
            var value = argument();
            if (value == null) return Text("null");
            if (value is CancellationToken) return Text("");
            if (value is StepArgument structured)
            {
                structuredArgument?.Invoke(structured);
                return Text(structured.ToString() ?? "null");
            }
            if (converters != null)
                foreach (var converter in converters)
                    if (converter.Matches(value)) return Text(converter.Convert(value) ?? "null");
            // Convert once per argument, even when a template repeats the placeholder.
            if (value is Delegate || value is string) return Text(value.ToString() ?? "null");
            if (value is IDictionary dictionary)
            {
                var entries = new List<string>();
                foreach (DictionaryEntry entry in dictionary) entries.Add($"{entry.Key}={entry.Value}");
                return Text(string.Join(",", entries));
            }
            if (value is IEnumerable enumerable) return Text(string.Join(", ", enumerable.Cast<object>()));
            if (value is IFormattable) return new ArgumentValue(value, null, name, diagnostic);
            return Text(value.ToString() ?? "null");
        }
        catch (Exception exception)
        {
            return Text(ReportError(exception, name, diagnostic));
        }

        ArgumentValue Text(string text) => new(null, text, name, diagnostic);
    }

    private static string ReportError(Exception exception, string? name, Action<string>? diagnostic)
    {
        while (exception is TargetInvocationException && exception.InnerException != null) exception = exception.InnerException;
        var message = $"Step text argument '{name}' could not be evaluated: {exception.GetType().Name}: {exception.Message}";
        Debug.WriteLine(message);
        diagnostic?.Invoke(message);
        return $"<error: {exception.GetType().Name}>";
    }

    private sealed class ArgumentValue
    {
        private readonly object? _value;
        private readonly string? _text;
        private readonly string? _name;
        private readonly Action<string>? _diagnostic;
        private readonly Dictionary<string, string> _formatted = new(StringComparer.Ordinal);

        internal ArgumentValue(object? value, string? text, string? name, Action<string>? diagnostic)
        {
            _value = value;
            _text = text;
            _name = name;
            _diagnostic = diagnostic;
        }

        internal string Format(string? format)
        {
            if (_text != null) return _text;
            if (_value == null) return "{" + _name + (format == null ? "" : ":" + format) + "}";
            var key = format ?? "";
            if (_formatted.TryGetValue(key, out var cached)) return cached;
            try { cached = ((IFormattable)_value).ToString(format, CultureInfo.InvariantCulture) ?? "null"; }
            catch (Exception exception) { cached = ReportError(exception, _name, _diagnostic); }
            _formatted[key] = cached;
            return cached;
        }
    }
}
