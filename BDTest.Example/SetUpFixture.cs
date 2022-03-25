using BDTest.Helpers;
using NUnit.Framework;

namespace BDTest.Example;

[SetUpFixture]
public class SetUpFixture
{
    [OneTimeTearDown]
    public void WriteJsonTestData()
    {
        var jsonData = BDTestJsonHelper.GetTestJsonData();
        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), $"JsonData-{Guid.NewGuid():N}.json"), jsonData);
    }
}