using System;

namespace BDTest.Attributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class TestInformationAttribute : Attribute
    {
        public string Information { get; }

        public TestInformationAttribute(string information)
        {
            Information = information;
        }
    }
}