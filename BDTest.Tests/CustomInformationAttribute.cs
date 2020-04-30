using BDTest.Attributes;

namespace BDTest.Tests
{
    public class CustomInformationAttribute : TestInformationAttribute
    {
        public CustomInformationAttribute(string information) : base(information)
        {
        }
    }
}