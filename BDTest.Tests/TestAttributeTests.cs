using System;
using System.IO;
using System.Linq;
using BDTest.Attributes;
using BDTest.ReportGenerator;
using BDTest.Test;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace BDTest.Tests
{
    public class TestAttributeTests : BDTestBase
    {
        [SetUp]
        public void Setup()
        {
            BDTestSettings.ReportFolderName = "CustomFolder";
            
            if (FileHelpers.HasCustomFolder())
            {
                Directory.Delete(FileHelpers.GetOutputFolder(), true);
            }
        }

        [Test]
        [TestInformation("Testing 1 Information Attribute")]
        public void TestInformationAttribute1()
        {
            var scenario = When(() => Console.WriteLine("my test has an attribute"))
                .Then(() => Console.WriteLine("the attribute should be serialized to the json output"))
                .BDTest();
            
            BDTestReportGenerator.Generate();
            
            Assert.That(scenario.CustomTestInformation.First().Print(), Is.EqualTo($"{nameof(TestInformationAttribute)} - Testing 1 Information Attribute"));
            Assert.That(GetDynamicJsonObject().SelectToken("$.Scenarios[0].CustomTestInformation[0]").ToString(), Is.EqualTo($"{nameof(TestInformationAttribute)} - Testing 1 Information Attribute"));
        }
        
        [Test]
        [TestInformation("Testing 1 Information Attribute")]
        [CustomInformation("Testing 2 Information Attributes")]
        public void TestInformationAttribute2()
        {
            var scenario = When(() => Console.WriteLine("my test has multiple attributes"))
                .Then(() => Console.WriteLine("the attributes should be serialized to the json output"))
                .BDTest();
            
            BDTestReportGenerator.Generate();
            
            Assert.That(scenario.CustomTestInformation.First().Print(), Is.EqualTo($"{nameof(TestInformationAttribute)} - Testing 1 Information Attribute"));
            Assert.That(GetDynamicJsonObject().SelectToken("$.Scenarios[0].CustomTestInformation[0]").ToString(), Is.EqualTo($"{nameof(TestInformationAttribute)} - Testing 1 Information Attribute"));
            Assert.That(scenario.CustomTestInformation[1].Print(), Is.EqualTo($"{nameof(CustomInformationAttribute)} - Testing 2 Information Attributes"));
            Assert.That(GetDynamicJsonObject().SelectToken("$.Scenarios[0].CustomTestInformation[1]").ToString(), Is.EqualTo($"{nameof(CustomInformationAttribute)} - Testing 2 Information Attributes"));
        }

        private static JObject GetDynamicJsonObject()
        {
            var jsonText = File.ReadAllText(FileHelpers.GetJsonFilePath());
            return JObject.Load(new JsonTextReader(new StringReader(jsonText)));
        }
    }
}