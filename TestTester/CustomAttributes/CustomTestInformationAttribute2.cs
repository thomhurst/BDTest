using BDTest.Attributes;

namespace TestTester.CustomAttributes
{
    public class CustomTestInformationAttribute2 : TestInformationAttribute
    {
        public CustomTestInformationAttribute2(string information) : base(information)
        {
        }
    }
}