using System;

namespace BDTest.Output
{
    [AttributeUsage(AttributeTargets.Field)]
    internal class EnumStringValueAttribute : Attribute
    {
        public string StringValue { get; set; }

        public EnumStringValueAttribute(string stringValue)
        {
            StringValue = stringValue;
        }
    }
}
