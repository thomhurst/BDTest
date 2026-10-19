namespace BDTest;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class BDTestRetryTearDownAttribute : Attribute
{
}