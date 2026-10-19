using System.Net;
using System.Threading;
using BDTest;
using NUnit.Framework;

namespace BDTest.Example;

[Story(AsA = "BDTest user",
    IWant = "to use either Given/When/Then or simple Step chaining",
    SoThat = "I can choose the most appropriate pattern for my tests")]
public class StepPatternComparison : NUnitBDTestBase<object>
{
    [Test]
    [ScenarioText("Traditional BDD test using Given/When/Then pattern")]
    public async Task TraditionalBDDPattern()
    {
        await Given(() => CreateAnAccount())
            .When(() => NavigateToHomePage())
            .Then(() => TheHttpStatusCodeIs(HttpStatusCode.OK))
            .BDTestAsync();
    }

    [Test]
    [ScenarioText("Simple test using Step chaining pattern")]
    public async Task StepChainingPattern()
    {
        await Step(() => CreateAnAccount())
            .Step(() => NavigateToHomePage())
            .Step(() => TheHttpStatusCodeIs(HttpStatusCode.OK))
            .BDTestAsync();
    }

    [Test]
    [ScenarioText("Complex workflow using Step chaining")]
    public async Task ComplexWorkflowWithSteps()
    {
        await Step(() => CreateAnAccount())
            .Step(() => NavigateToHomePage())
            .Step(() => Console.WriteLine("Additional processing..."))
            .Step(() => CreateAnAccount()) // Simulating another action
            .Step(() => TheHttpStatusCodeIs(HttpStatusCode.OK))
            .WithStepText(() => "Verify the final response status")
            .BDTestAsync();
    }

    [Test]
    [ScenarioText("Single step test")]
    public async Task SingleStepTest()
    {
        await Step(() => CreateAnAccount())
            .BDTestAsync();
    }

    [Test]
    [ScenarioText("Step pattern with custom step text")]
    public async Task StepWithCustomText()
    {
        await Step(() => CreateAnAccount())
            .WithStepText(() => "Setting up a new user account")
            .Step(() => NavigateToHomePage())
            .WithStepText(() => "Navigating to the main page")
            .BDTestAsync();
    }

    private HttpClient _client = null!;
    private HttpStatusCode _statusCode;

    [SetUp]
    public void CreateClient() => _client = new HttpClient(new ExampleResponseHandler());


    [TearDown]
    public void DisposeClient() => _client.Dispose();

    [StepText("I create a new account")]
    private async Task CreateAnAccount()
    {
        using var response = await _client.PostAsync("https://example.test/accounts", null);
        _statusCode = response.StatusCode;
    }

    [StepText("I navigate to the home page")]
    private async Task NavigateToHomePage()
    {
        using var response = await _client.GetAsync("https://example.test/");
        _statusCode = response.StatusCode;
    }

    [StepText("the HTTP status code is {statusCode}")]
    private void TheHttpStatusCodeIs(HttpStatusCode statusCode) =>
        Assert.That(_statusCode, Is.EqualTo(statusCode));

    // Keep these runnable examples independent of public websites and network access.
    private sealed class ExampleResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}
