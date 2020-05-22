using System;

namespace BDTest.Settings
{
    public class SkipStepRule
    {
        public Func<bool> Condition { get; }
        public Type AssociatedSkipAttributeType { get; }
        
        public SkipStepRule(Type associatedSkipAttributeType, Func<bool> condition)
        {
            AssociatedSkipAttributeType = associatedSkipAttributeType;
            Condition = condition;
        }
    }
}