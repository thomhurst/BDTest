using System;
using System.Collections.Generic;
using BDTest.Attributes;

namespace BDTest.Settings.Skip;

public class SkipStepRules
{
    internal SkipStepRules()
    {
    }
        
    internal List<SkipStepRule<object>> Rules = new();

    public void Add<T>(Func<T, bool> condition) where T : SkipStepAttribute
    {
        // Cast to Func<object, bool>
        Func<object, bool> func = value => condition((T) value);
        Rules.Add(new SkipStepRule<object>(typeof(T), func));
    }
}