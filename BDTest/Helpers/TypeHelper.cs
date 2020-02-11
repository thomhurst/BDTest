using System;
using System.Collections.Generic;

namespace BDTest.Helpers
{
    public class TypeHelper
    {
        private static readonly HashSet<Type> FuncActionSet = new HashSet<Type>
        {
            typeof(Action), typeof(Action<>), typeof(Action<,>),    
            typeof(Func<>), typeof(Func<,>), typeof(Func<,,>),      
        };

        public static bool IsFuncOrAction(Type type)
        {
            return FuncActionSet.Contains(type) ||
                   type.IsGenericType && FuncActionSet.Contains(type.GetGenericTypeDefinition());
        }
    }
}