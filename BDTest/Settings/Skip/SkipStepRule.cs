using System;

namespace BDTest.Settings.Skip
{
    public class SkipStepRule<T>
    {
        public Func<T, bool> Condition { get; }
        public Type AssociatedSkipAttributeType { get; }
        
        public SkipStepRule(Type associatedSkipAttributeType, Func<T, bool> condition)
        {
            AssociatedSkipAttributeType = associatedSkipAttributeType;
            Condition = condition;
        }
    }
}