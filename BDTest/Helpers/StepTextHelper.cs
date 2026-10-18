using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using BDTest.Attributes;
using BDTest.Interfaces;
using BDTest.Settings;
using BDTest.Test;

namespace BDTest.Helpers;

public static class StepTextHelper
{
    /// <summary>Renders a selected step method using explicit, lazy argument readers.</summary>
    public static string GetStepText(MethodInfo method, IReadOnlyList<Func<object>> arguments,
        bool omitCancellationToken = false, Action<string> diagnostic = null)
    {
        if (method == null) throw new ArgumentNullException(nameof(method));
        return GetStepText(method, arguments, omitCancellationToken, diagnostic, evaluateArguments: true);
    }

    internal static string GetStepText(MethodInfo method, IReadOnlyList<Func<object>> arguments,
        bool omitCancellationToken, Action<string> diagnostic, bool evaluateArguments,
        IReadOnlyList<StepTextConverterRegistration> converters = null, Action<StepArgument> structuredArgument = null)
    {
        var parameters = method.GetParameters();
        var visibleParameters = parameters.Where(p => !omitCancellationToken || p.ParameterType != typeof(CancellationToken)).ToArray();
        if (evaluateArguments && arguments != null && arguments.Count != parameters.Length && arguments.Count != visibleParameters.Length)
        {
            throw new ArgumentException($"Expected {visibleParameters.Length} step text argument readers, but received {arguments.Count}.", nameof(arguments));
        }

        var values = visibleParameters.Select((parameter, index) =>
        {
            var argumentIndex = arguments?.Count == parameters.Length ? parameter.Position : index;
            return evaluateArguments && arguments != null
                ? GetArgumentValue(arguments[argumentIndex], parameter.Name, diagnostic, converters, structuredArgument)
                : $"{{{parameter.Name}}}";
        }).ToArray();

        var attribute = method.GetCustomAttribute<StepTextAttribute>(inherit: true);
        if (attribute?.Text == null)
        {
            var name = NameHumanizer.Humanize(method.Name);
            if (!string.IsNullOrWhiteSpace(name) && char.IsUpper(name[0]))
            {
                name = char.ToLowerInvariant(name[0]) + name.Substring(1);
            }
            return $"{name} {string.Join(" ", values)}".Trim();
        }

        var template = attribute.Text;
        try
        {
            foreach (var parameterOverride in attribute.StepTextParameterOverrides)
            {
                var parts = parameterOverride.Split(new[] { ':' }, 2);
                template = template.Replace($"{{{parts[0]}}}", parts[1]);
            }
            return string.Format(template, values.Cast<object>().ToArray());
        }
        catch (Exception exception)
        {
            throw new ArgumentException(
                $"Step Text arguments are wrong.{Environment.NewLine}Template is: {template}{Environment.NewLine}Arguments are {string.Join(",", values)}", exception);
        }
    }

    internal static string GetArgumentValue(Func<object> argument, string name, Action<string> diagnostic,
        IReadOnlyList<StepTextConverterRegistration> converters = null, Action<StepArgument> structuredArgument = null)
    {
        try
        {
            var argumentValue = argument();
            if (argumentValue == null)
            {
                return "null";
            }

            if (argumentValue is StepArgument structured)
            {
                structuredArgument?.Invoke(structured);
                return structured.ToString();
            }

            if (converters != null)
            {
                foreach (var converter in converters)
                {
                    if (converter.Matches(argumentValue)) return converter.Convert(argumentValue) ?? "null";
                }
            }
            else
            {
                foreach (var converter in BDTestSettings.GetCustomStringConvertersSnapshot())
                {
                    var converterInterface = converter.GetType().GetInterfaces()
                        .Where(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IStepTextStringConverter<>))
                        .Where(type => type.GetGenericArguments()[0].IsInstanceOfType(argumentValue))
                        .OrderByDescending(type => type.GetGenericArguments()[0] == argumentValue.GetType())
                        .FirstOrDefault();

                    if (converterInterface != null)
                    {
                        // Invoke through the interface to support explicit implementations.
                        var method = converterInterface.GetMethod(nameof(IStepTextStringConverter<object>.ConvertToString));
                        return method.Invoke(converter, new[] { argumentValue }) as string ?? "null";
                    }
                }
            }

            // Delegate arguments are values, not callbacks for generating step text.
            if (argumentValue is Delegate || argumentValue is string)
            {
                return argumentValue.ToString();
            }

            if (argumentValue is IDictionary dictionary)
            {
                var entries = new List<string>();
                foreach (DictionaryEntry entry in dictionary)
                {
                    entries.Add($"{entry.Key}={entry.Value}");
                }
                return string.Join(",", entries);
            }

            if (argumentValue is IEnumerable enumerable)
            {
                return string.Join(", ", enumerable.Cast<object>());
            }

            return argumentValue.ToString() ?? "null";
        }
        catch (Exception exception)
        {
            while (exception is TargetInvocationException && exception.InnerException != null)
            {
                exception = exception.InnerException;
            }
            var message = $"Step text argument '{name}' could not be evaluated: {exception.GetType().Name}: {exception.Message}";
            Debug.WriteLine(message);
            diagnostic?.Invoke(message);
            return $"<error: {exception.GetType().Name}>";
        }
    }
}
