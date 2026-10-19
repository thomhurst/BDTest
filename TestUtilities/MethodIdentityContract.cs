using BDTest.Test;

namespace BDTest.TestUtilities;

public static class MethodIdentityContract
{
    // Construction deliberately happens in a helper after an optional suspension.
    // Only the adapter's method identity can supply the test attributes.
    public static async Task Verify(BDTestBase fixture, string text, bool suspend)
    {
        if (suspend) await Task.Yield();
        var attempts = 0;
        var scenario = await fixture.When((Action)(() =>
        {
            if (++attempts == 1) throw new InvalidOperationException("Retry the identified method.");
        })).Then(() => { }).BDTestAsync();

        if (scenario.ScenarioText != text || scenario.RetryCount != 1 || attempts != 2
            || (scenario.CustomTestInformation ?? throw new InvalidOperationException("Expected scenario method information.")).Single() != text
            || !scenario.Tags.Contains("fixture") || !scenario.Tags.Contains(text))
            throw new InvalidOperationException("The adapter did not preserve test method metadata: " + text);
    }
}
