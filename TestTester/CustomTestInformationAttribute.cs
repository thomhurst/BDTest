using BDTest.Attributes;

namespace TestTester
{
    public class CustomTestInformationAttribute : TestInformationAttribute
    {
        public CustomTestInformationAttribute(string information) : base(information)
        {
        }
    }
}