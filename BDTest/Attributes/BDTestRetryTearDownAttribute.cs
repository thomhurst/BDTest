using System;

namespace BDTest.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class BDTestRetryTearDownAttribute : Attribute
{
}