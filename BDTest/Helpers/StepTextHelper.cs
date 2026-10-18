using System.Collections;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using BDTest.Attributes;
using BDTest.Interfaces;
using BDTest.Settings;
using Humanizer;

namespace BDTest.Helpers;

public static class StepTextHelper
{
    public static string GetStepText(Expression<Action> action)
    {
        return GetStepText(action?.Body as MethodCallExpression);
    }
        
    public static string GetStepText(Expression<Func<Task>> action)
    {
        return GetStepText(action?.Body as MethodCallExpression);
    }
        
    public static string GetStepText(Expression expression)
    {
        return GetStepText(expression as MethodCallExpression);
    }
        
    internal static string GetStepText(Expression expression, bool omitCancellationToken)
    {
        return GetStepText(expression as MethodCallExpression, omitCancellationToken);
    }

    internal static string GetStepTextTemplate(Expression expression, bool omitCancellationToken)
    {
        return GetStepText(expression as MethodCallExpression, omitCancellationToken, evaluateArguments: false);
    }

    internal static string GetStepText(Expression expression, bool omitCancellationToken, Action<string> diagnostic)
    {
        return GetStepText(expression as MethodCallExpression, omitCancellationToken, diagnostic);
    }

    private static string GetStepText(MethodCallExpression methodCallExpression, bool omitCancellationToken = false,
        Action<string> diagnostic = null, bool evaluateArguments = true)
    {
        if (methodCallExpression == null)
        {
            return string.Empty;
        }
            
        var arguments = GetMethodArguments(methodCallExpression, omitCancellationToken, evaluateArguments, diagnostic);

        var methodInfo = methodCallExpression.Method;

        var stepTextAttribute = (StepTextAttribute) methodInfo.GetCustomAttributes(typeof(StepTextAttribute), true).FirstOrDefault();
            
        var customStepText =
            stepTextAttribute?.Text;

        var methodNameHumanized = $"{LowercaseFirstLetter(methodInfo.Name.Humanize())} {string.Join(" ", arguments)}".Trim();

        if (customStepText != null)
        {
            try
            {
                customStepText = UseStepTextParameterOverrides(stepTextAttribute);
                customStepText = string.Format(customStepText, arguments.Cast<object>().ToArray());
            }
            catch (Exception exception)
            {
                throw new ArgumentException(
                    $"Step Text arguments are wrong.{Environment.NewLine}Template is: {customStepText}{Environment.NewLine}Arguments are {string.Join(",", arguments)}", exception);
            }
        }

        return customStepText ?? methodNameHumanized;
    }

    private static string LowercaseFirstLetter(string input)
    {
        if (!string.IsNullOrWhiteSpace(input) && char.IsUpper(input[0]))
        {
            return  char.ToLower(input[0]) + input.Substring(1);
        }

        return input;
    }
        
    private static List<string> GetMethodArguments(MethodCallExpression methodCallExpression, bool omitCancellationToken,
        bool evaluateArguments, Action<string> diagnostic)
    {
        var parameters = methodCallExpression.Method.GetParameters();
        return methodCallExpression.Arguments.Select((argument, index) => new { Expression = argument, Name = parameters[index].Name })
            .Where(argument => !omitCancellationToken || argument.Expression.Type != typeof(CancellationToken))
            .Select(argument => evaluateArguments
                ? GetExpressionValue(argument.Expression, argument.Name, diagnostic) ?? "null"
                : $"{{{argument.Name}}}")
            .ToList();
    }

    private static string GetExpressionValue(Expression argument, string name, Action<string> diagnostic)
    {
        try
        {
            var argumentValue = argument is ConstantExpression constantExpression
                ? constantExpression.Value
                : Expression.Lambda(Expression.Convert(argument, argument.Type)).Compile().DynamicInvoke();

            if (argumentValue == null)
            {
                return "null";
            }

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
                    return method.Invoke(converter, new[] { argumentValue }) as string;
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

            return argumentValue.ToString();
        }
        catch (Exception e)
        {
            // DynamicInvoke and reflection wrap user exceptions; report the useful cause.
            while (e is TargetInvocationException && e.InnerException != null)
            {
                e = e.InnerException;
            }
            var message = $"Step text argument '{name}' could not be evaluated: {e.GetType().Name}: {e.Message}";
            Debug.WriteLine(message);
            diagnostic?.Invoke(message);
            return $"<error: {e.GetType().Name}>";
        }
    }

    private static string UseStepTextParameterOverrides(StepTextAttribute stepTextAttribute)
    {
        return stepTextAttribute.StepTextParameterOverrides.Aggregate(stepTextAttribute.Text, (current, parameterOverride) => current.Replace($"{{{parameterOverride.Split(':')[0]}}}", parameterOverride.Split(new [] {':'}, 2)[1]));
    }
}
