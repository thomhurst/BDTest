using BDTest.Example.Context;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class ExampleApiContextTests
{
    [TestCase("id")]
    [TestCase("ID")]
    [TestCase("Id")]
    public async Task Response_properties_match_without_case_sensitivity(string propertyName)
    {
        var context = new MyApiContext();
        using var client = context.HttpClient;
        using var response = new HttpResponseMessage
        {
            Content = new StringContent($$"""{"{{propertyName}}":"123"}""")
        };
        typeof(MyApiContext).GetProperty(nameof(MyApiContext.ResponseMessage))!.SetValue(context, response);

        var result = await context.DeserializeResponse<ApiResponse>();

        Assert.That(result.Id, Is.EqualTo("123"));
    }

    public sealed class ApiResponse
    {
        public string Id { get; set; }
    }
}
