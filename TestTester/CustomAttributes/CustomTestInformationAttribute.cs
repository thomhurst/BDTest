using BDTest.Attributes;

namespace TestTester.CustomAttributes
{
    public class CustomTestInformationAttribute : TestInformationAttribute
    {
        public CustomTestInformationAttribute(string information) : base(information)
        {
        }
    }
}