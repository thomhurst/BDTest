using System.Linq;
using BDTest.Output;

namespace BDTest.Test.Steps
{
    public enum StepType
    {
        [EnumStringValue("Given")]
        Given,

        [EnumStringValue("And")]
        AndGiven,

        [EnumStringValue("When")]
        When,

        [EnumStringValue("Then")]
        Then,

        [EnumStringValue("And")]
        AndThen
    }

    public static class StepTypeExtensions
    {
        public static string GetValue(this StepType stepType)
        {
            var type = stepType.GetType();

            var fieldInfo = type.GetField(stepType.ToString());

            var attributes = fieldInfo.GetCustomAttributes(
                                 typeof(EnumStringValueAttribute), false) as EnumStringValueAttribute[] ?? new EnumStringValueAttribute[] { };

            return attributes.FirstOrDefault()?.StringValue;
        }
    }
}
