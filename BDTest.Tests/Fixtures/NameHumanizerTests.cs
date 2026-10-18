using System.Reflection;
using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Metadata;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class NameHumanizerTests
{
    [TestCase("Given_a_user_logs_in", "Given a user logs in")]
    [TestCase("given_a_user_logs_in", "Given a user logs in")]
    [TestCase("When_HTTP_request", "When HTTP request")]
    [TestCase("when_HTTPRequest_arrives", "When HTTPRequest arrives")]
    [TestCase("__Given__a_user__", "Given a user")]
    [TestCase("UserLogsIn", "User logs in")]
    [TestCase("userLogsIn", "User logs in")]
    [TestCase("MFAToken", "MFA token")]
    [TestCase("UserMFAIsEnabled", "User MFA is enabled")]
    [TestCase("UserLogsInWithMFA", "User logs in with MFA")]
    [TestCase("HTTP", "HTTP")]
    [TestCase("Order2IsShipped", "Order 2 is shipped")]
    [TestCase("HTTP404Response", "HTTP 404 response")]
    [TestCase("Version42beta", "Version 42 beta")]
    [TestCase("42Orders", "42 orders")]
    [TestCase("X", "X")]
    [TestCase("user", "User")]
    [TestCase("USER", "USER")]
    [TestCase("MoveNext", "Move next")]
    [TestCase("<RunAsync>d__12", "<RunAsync>d__12")]
    [TestCase("<Run>b__0_1", "<Run>b__0_1")]
    [TestCase("Namespace.IFoo.RunAsync", "Namespace.IFoo.RunAsync")]
    [TestCase(".ctor", ".ctor")]
    [TestCase("___", "___")]
    [TestCase("", "")]
    [TestCase(null, null)]
    public void Scenario_fallback_uses_the_documented_rules(string name, string expected)
    {
        Assert.That(NameHumanizer.Humanize(name), Is.EqualTo(expected));
        if (!string.IsNullOrEmpty(name))
        {
            Assert.That(ScenarioTextHelper.GetScenarioText(null, name), Is.EqualTo(expected));
        }
    }

    // Frozen Humanizer.Core 3.0.10 output, captured before removing the dependency.
    // This representative corpus includes test and sample methods, acronyms, digits,
    // underscore/PascalCase mixtures and ordinary names. No corpus entries differ.
    [TestCase("TestPass", "Test pass")]
    [TestCase("TestFail", "Test fail")]
    [TestCase("CreateAnAccount", "Create an account")]
    [TestCase("TheHttpStatusCodeIs", "The http status code is")]
    [TestCase("NoScenarioText", "No scenario text")]
    [TestCase("No_Scenario_Text_with_Hyphens", "No Scenario Text with Hyphens")]
    [TestCase("Test1", "Test 1")]
    [TestCase("NotRunTest3", "Not run test 3")]
    [TestCase("More_than_50_entries_are_rejected_even_when_duplicated", "More than 50 entries are rejected even when duplicated")]
    [TestCase("RetryCreatesAndConfiguresANewContextOnce", "Retry creates and configures a new context once")]
    [TestCase("NUNitSuccessException", "NU nit success exception")]
    [TestCase("NUNitIgnoreException", "NU nit ignore exception")]
    [TestCase("CustomHtmlIsIsolatedInAScriptlessFrame", "Custom html is isolated in a scriptless frame")]
    [TestCase("ZZZ_EnsureContextsAreUnique", "ZZZ EnsureContextsAreUnique")]
    [TestCase("Duplicated_values_fit_the_plain_JSON_upload_size", "Duplicated values fit the plain JSON upload size")]
    [TestCase("Unicode_reports_use_the_exact_UTF8_byte_budget", "Unicode reports use the exact UTF8 byte budget")]
    [TestCase("Host_JSON_defaults_do_not_change_report_capacity", "Host JSON defaults do not change report capacity")]
    [TestCase("Invalid_numeric_statuses_fail_with_a_JSON_error", "Invalid numeric statuses fail with a JSON error")]
    [TestCase("Registration_preserves_host_MVC_formatters_and_JSON_options", "Registration preserves host MVC formatters and JSON options")]
    [TestCase("Raw_HTTP_download_preserves_storage_bytes_and_MVC_disposes_stream", "Raw HTTP download preserves storage bytes and MVC disposes stream")]
    [TestCase("MissingRawJsonReportReturns404WithoutHtml", "Missing raw json report returns 404 without html")]
    [TestCase("Upload_and_raw_download_ignore_host_JSON_options", "Upload and raw download ignore host JSON options")]
    [TestCase("NoHooksIsANoOp", "No hooks is a no op")]
    [TestCase("BDTestRetrySetup", "BD test retry setup")]
    [TestCase("BDTestRetryTearDown", "BD test retry tear down")]
    [TestCase("SetUp", "Set up")]
    [TestCase("TearDown", "Tear down")]
    [TestCase("TestWithContext1", "Test with context 1")]
    [TestCase("InterceptConsoleOutput_Synchronous_And_Asynchronous_Steps_Mix1_Called_Asynchronously",
        "InterceptConsoleOutput Synchronous And Asynchronous Steps Mix1 Called Asynchronously")]
    public void Repository_corpus_matches_frozen_Humanizer_output(string name, string humanizerOutput)
    {
        Assert.That(ScenarioTextHelper.GetScenarioText(null, name), Is.EqualTo(humanizerOutput));
    }

    // Intentional differences from Humanizer.Core 3.0.10, also listed in the migration notes.
    [TestCase("given_a_user_logs_in", "given a user logs in", "Given a user logs in")]
    [TestCase("when_HTTPRequest_arrives", "when HTTPRequest arrives", "When HTTPRequest arrives")]
    [TestCase("__Given__a_user__", "  Given  a user  ", "Given a user")]
    [TestCase("Version42beta", "Version 42beta", "Version 42 beta")]
    [TestCase("<RunAsync>d__12", "<RunAsync>d  12", "<RunAsync>d__12")]
    [TestCase("<Run>b__0_1", "<Run>b  0 1", "<Run>b__0_1")]
    [TestCase("___", "   ", "___")]
    public void Documented_differences_from_Humanizer(string name, string humanizerOutput, string expected)
    {
        Assert.That(expected, Is.Not.EqualTo(humanizerOutput));
        Assert.That(NameHumanizer.Humanize(name), Is.EqualTo(expected));
    }

    [TestCase(nameof(user_logs_in), "User logs in", "user logs in")]
    [TestCase(nameof(MFAToken), "MFA token", "mFA token")]
    [TestCase(nameof(Order2IsShipped), "Order 2 is shipped", "order 2 is shipped")]
    [TestCase(nameof(ExplicitText), "Exact ScenarioText", "Exact StepText")]
    public void Metadata_and_steps_use_the_same_rules_and_preserve_explicit_text(string name, string scenarioText, string stepText)
    {
        var method = typeof(NameHumanizerTests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
        var metadata = TestMethodMetadata.Get(typeof(NameHumanizerTests), method);
        Assert.That(ScenarioTextHelper.GetScenarioText(metadata, "IgnoredCaller"), Is.EqualTo(scenarioText));
        Assert.That(StepTextHelper.GetStepText(method, Array.Empty<Func<object>>()), Is.EqualTo(stepText));
    }

    [Test, SetCulture("tr-TR")]
    public void Generated_text_is_culture_independent()
    {
        Assert.That(ScenarioTextHelper.GetScenarioText(null, "itemIsValid"), Is.EqualTo("Item is valid"));
        Assert.That(ScenarioTextHelper.GetScenarioText(null, "ItemIsValid"), Is.EqualTo("Item is valid"));
        var method = typeof(NameHumanizerTests).GetMethod(nameof(ItemIsValid), BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(StepTextHelper.GetStepText(method, Array.Empty<Func<object>>()), Is.EqualTo("item is valid"));
    }

    [Test]
    public void Core_does_not_reference_Humanizer_or_export_the_splitter()
    {
        var assembly = typeof(StepTextHelper).Assembly;
        Assert.That(assembly.GetReferencedAssemblies().Select(name => name.Name), Does.Not.Contain("Humanizer"));
        Assert.That(assembly.GetExportedTypes().Select(type => type.Name), Does.Not.Contain("NameHumanizer"));
    }

    private static void user_logs_in() { }
    private static void MFAToken() { }
    private static void Order2IsShipped() { }
    private static void ItemIsValid() { }

    [ScenarioText("Exact ScenarioText"), StepText("Exact StepText")]
    private static void ExplicitText() { }
}
