using System;
using System.Collections.Generic;

namespace BDTest.Settings.Retry
{
    public class RetryTestRules
    {
        internal RetryTestRules()
        {
        }
        
        internal List<RetryTestRule> Rules = new List<RetryTestRule>();

        public void Add(Func<Exception, bool> condition, int retryLimit)
        {
            Rules.Add(new RetryTestRule(condition, retryLimit));
        }
    }

    public class RetryTestRule
    {
        internal Func<Exception, bool> Condition { get; }
        public int RetryLimit { get; }

        internal RetryTestRule(Func<Exception, bool> condition, int retryLimit)
        {
            Condition = condition;
            RetryLimit = retryLimit;
        }
    }
}