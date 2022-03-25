using System;
using BDTest.Helpers.JsonConverters;
using Newtonsoft.Json;

namespace BDTest.Attributes;

[JsonConverter(typeof(TestInformationAttributeJsonConverter))]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class TestInformationAttribute : Attribute
{
    public string Information { get; }
    public string Print() => $"{GetType().Name} - {Information}";

    public TestInformationAttribute(string information)
    {
        Information = information;
    }
}