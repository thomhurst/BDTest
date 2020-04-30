using System;
using BDTest.Helpers;
using Newtonsoft.Json;

namespace BDTest.Attributes
{
    [JsonConverter(typeof(TestInformationAttributeJsonConverter))]
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class TestInformationAttribute : Attribute
    {
        private readonly string _information;
        public string Print() => $"{GetType().Name} - {_information}";

        public TestInformationAttribute(string information)
        {
            _information = information;
        }
    }
}