using System;
using System.Collections;
using System.Collections.Generic;

namespace BDTest.Helpers;

internal static class TypeHelper
{
    private static readonly HashSet<Type> FuncActionSet = new()
    {
        typeof(Action),
        typeof(Action<>),
        typeof(Action<,>),
        typeof(Func<>),
        typeof(Func<,>),
        typeof(Func<,,>)
    };

    public static bool IsFuncOrAction(Type type)
    {
        return FuncActionSet.Contains(type) || IsGenericFuncOrAction();

        bool IsGenericFuncOrAction()
        {
            return type.IsGenericType && FuncActionSet.Contains(type.GetGenericTypeDefinition());
        }
    }

    public static bool IsIEnumerable<T>(T value)
    {
        return value is IEnumerable && value.GetType().IsGenericType;
    }

    public static bool IsIDictionary<T>(T value)
    {
        return value is IDictionary && value.GetType().IsGenericType;
    }
}