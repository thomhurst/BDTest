using System.Diagnostics;
using System.Linq.Expressions;
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
        
    private static string GetStepText(MethodCallExpression methodCallExpression)
    {
        if (methodCallExpression == null)
        {
            return string.Empty;
        }
            
        var arguments = GetMethodArguments(methodCallExpression);

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
            catch (Exception)
            {
                throw new ArgumentException(
                    $"Step Text arguments are wrong.{Environment.NewLine}Template is: {customStepText}{Environment.NewLine}Arguments are {string.Join(",", arguments)}");
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
        
    private static List<string> GetMethodArguments(MethodCallExpression methodCallExpression)
    {
        return methodCallExpression?.Arguments != null 
            ? methodCallExpression.Arguments.Select(GetExpressionValue).Select(value => value ?? "null").ToList() 
            : new List<string>();
    }
        
    private static string GetExpressionValue(Expression argument)
    {
        try
        {
            var argumentValue = argument is ConstantExpression constantExpression
                ? constantExpression.Value?.ToString()
                : Expression.Lambda(Expression.Convert(argument, argument.Type)).Compile().DynamicInvoke();

            if (argumentValue == null)
            {
                return "null";
            }

            var stepTextStringConverter = BDTestSettings.CustomStringConverters
                .FirstOrDefault(type => 
                    type.GetType().GetInterfaces().FirstOrDefault()?.Name.StartsWith(nameof(IStepTextStringConverter<object>)) == true 
                    && type.GetType().GetInterfaces().FirstOrDefault()?.GetGenericArguments().FirstOrDefault() == argumentValue.GetType());
                
            if (stepTextStringConverter != null)
            {
                var method = stepTextStringConverter.GetType().GetMethod(nameof(IStepTextStringConverter<object>.ConvertToString));
                if (method != null)
                {
                    return method.Invoke(stepTextStringConverter, new[] { argumentValue }) as string;
                }
            }

            if (TypeHelper.IsFuncOrAction(argumentValue.GetType()))
            {
                var func = (Delegate) argumentValue;

                return func.DynamicInvoke()?.ToString();
            }

            if (TypeHelper.IsIEnumerable(argumentValue) || argumentValue.GetType().IsArray)
            {
                return string.Join(", ", (IEnumerable<object>) argumentValue);
            }

            if (TypeHelper.IsIDictionary(argumentValue))
            {
                return string.Join(",",
                    ((IDictionary<object, object>) argumentValue).Select(kv => $"{kv.Key}={kv.Value}")
                );
            }

            return argumentValue.ToString();
        }
        catch (Exception e)
        {
            Debug.WriteLine($"BDTest Exception:{Environment.NewLine}Class: {nameof(StepTextHelper)}{Environment.NewLine}Method: {nameof(GetExpressionValue)}{Environment.NewLine}Exception: {e.Message}{Environment.NewLine}{e.StackTrace}");
            return "null";
        }
    }

    private static string UseStepTextParameterOverrides(StepTextAttribute stepTextAttribute)
    {
        return stepTextAttribute.StepTextParameterOverrides.Aggregate(stepTextAttribute.Text, (current, parameterOverride) => current.Replace($"{{{parameterOverride.Split(':')[0]}}}", parameterOverride.Split(new [] {':'}, 2)[1]));
    }
}