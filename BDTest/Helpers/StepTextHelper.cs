using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using BDTest.Attributes;
using Humanizer;

namespace BDTest.Helpers
{
    public static class StepTextHelper
    {
        public static string GetStepText(Expression<Action> action)
        {
            return GetStepText(action.Body as MethodCallExpression);
        }
        
        public static string GetStepText(Expression<Func<Task>> action)
        {
            return GetStepText(action.Body as MethodCallExpression);
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

            var methodInfo = methodCallExpression?.Method;

            var stepTextAttribute = (StepTextAttribute)(methodInfo?.GetCustomAttributes(
                                                            typeof(StepTextAttribute), true) ??
                                                        new string[] { }).FirstOrDefault();
            
            var customStepText =
                stepTextAttribute?.Text;
            
            var methodNameHumanized = methodInfo?.Name.Humanize();

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
                        $"Step Text arguments are wrong.\nTemplate is: {customStepText}\nArguments are {string.Join(",", arguments)}");
                }
            }

            return customStepText ?? methodNameHumanized;
        }
        
        private static List<string> GetMethodArguments(MethodCallExpression methodCallExpression)
        {
            var arguments = new List<string>();

            if (methodCallExpression?.Arguments != null)
            {
                foreach (var argument in methodCallExpression.Arguments)
                {
                    var value = GetExpressionValue(argument);
                    arguments.Add(value ?? "null");
                }
            }

            return arguments;
        }
        
        private static string GetExpressionValue(Expression argument)
        {
            try
            {
                var compiledExpression = Expression.Lambda(argument).Compile().DynamicInvoke();

                if (compiledExpression == null)
                {
                    return "null";
                }

                if (TypeHelper.IsFuncOrAction(compiledExpression.GetType()))
                {
                    var func = (Delegate) compiledExpression;

                    return func.DynamicInvoke()?.ToString();
                }

                if (TypeHelper.IsIEnumerable(compiledExpression) || compiledExpression.GetType().IsArray)
                {
                    return string.Join(", ", (IEnumerable<object>) compiledExpression);
                }

                if (TypeHelper.IsIDictionary(compiledExpression))
                {
                    return string.Join(",",
                        ((IDictionary<object, object>) compiledExpression).Select(kv => $"{kv.Key}={kv.Value}")
                    );
                }

                return compiledExpression.ToString();
            }
            catch (Exception)
            {
                return "null";
            }
        }

        private static string UseStepTextParameterOverrides(StepTextAttribute stepTextAttribute)
        {
            return stepTextAttribute.StepTextParameterOverrides.Aggregate(stepTextAttribute.Text, (current, parameterOverride) => current.Replace($"{{{parameterOverride.Split(':')[0]}}}", parameterOverride.Split(new [] {':'}, 2)[1]));
        }
    }
}