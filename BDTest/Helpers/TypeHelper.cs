using System;
using System.Collections;
using System.Collections.Generic;

namespace BDTest.Helpers
{
    internal static class TypeHelper
    {
        private static readonly HashSet<Type> FuncActionSet = new HashSet<Type>
        {
            typeof(Action),
            typeof(Action<>),
            typeof(Action<,>),
            typeof(Func<>),
            typeof(Func<,>),
            typeof(Func<,,>),
        };

        private static readonly Type DictionaryType = typeof(IDictionary<,>);

        private static readonly Type IEnumerableType = typeof(IEnumerable<>);

        private static readonly Type IListType = typeof(IList<>);

        private static readonly Type ICollectionType = typeof(ICollection<>);

        public static bool IsFuncOrAction(Type type)
        {
            return FuncActionSet.Contains(type) ||
                   type.IsGenericType && FuncActionSet.Contains(type.GetGenericTypeDefinition());
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
}