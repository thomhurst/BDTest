using System;
using System.IO;
using BDTest.Helpers;
using NUnit.Framework;

namespace BDTest.Example
{
    [SetUpFixture]
    public class SetUpFixture
    {
        [OneTimeTearDown]
        public void GetJsonTestData()
        {
            var jsonData = BDTestJsonHelper.GetTestJsonData();
            File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), $"JsonData-{Guid.NewGuid():N}.json"), jsonData);
        }
    }
}