using System;

namespace BDTest.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = false, Inherited = true)]
public class BDTestRetryAttribute : Attribute
{
    public int Count { get; }

    public BDTestRetryAttribute(int count)
    {
        Count = count;
    }
}