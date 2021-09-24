using System;
using System.Collections.Generic;
using BDTest.Test;

namespace BDTest.Settings.Skip
{
    public class SkipTestRules
    {
        internal SkipTestRules()
        {
        }
        
        internal List<Func<Scenario, bool>> Rules = new();

        public void Add(Func<Scenario, bool> condition)
        {
            Rules.Add(condition);
        }
    }
}