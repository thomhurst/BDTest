using System.Reflection;
using BDTest;
using BDTest.Helpers;
using BDTest.Metadata;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

public class NameHumanizerTests
{
    [Test]
    [Arguments("Given_a_user_logs_in", "Given a user logs in")]
    [Arguments("given_a_user_logs_in", "Given a user logs in")]
    [Arguments("When_HTTP_request", "When HTTP request")]
    [Arguments("when_HTTPRequest_arrives", "When HTTPRequest arrives")]
    [Arguments("__Given__a_user__", "Given a user")]
    [Arguments("UserLogsIn", "User logs in")]
    [Arguments("userLogsIn", "User logs in")]
    [Arguments("MFAToken", "MFA token")]
    [Arguments("UserMFAIsEnabled", "User MFA is enabled")]
    [Arguments("UserLogsInWithMFA", "User logs in with MFA")]
    [Arguments("HTTP", "HTTP")]
    [Arguments("Order2IsShipped", "Order 2 is shipped")]
    [Arguments("HTTP404Response", "HTTP 404 response")]
    [Arguments("Version42beta", "Version 42 beta")]
    [Arguments("42Orders", "42 orders")]
    [Arguments("X", "X")]
    [Arguments("user", "User")]
    [Arguments("USER", "USER")]
    [Arguments("MoveNext", "Move next")]
    [Arguments("<RunAsync>d__12", "<RunAsync>d__12")]
    [Arguments("<Run>b__0_1", "<Run>b__0_1")]
    [Arguments("Namespace.IFoo.RunAsync", "Namespace.IFoo.RunAsync")]
    [Arguments(".ctor", ".ctor")]
    [Arguments("___", "___")]
    [Arguments("", "")]
    [Arguments(null, null)]
    public async Task Scenario_fallback_uses_the_documented_rules(string? name, string? expected)
    {
        await Assert.That(NameHumanizer.Humanize(name)).IsEqualTo(expected);
        if (!string.IsNullOrEmpty(name))
        {
            await Assert.That(ScenarioTextHelper.GetScenarioText(null, name)).IsEqualTo(expected);
        }
    }

    // Frozen Humanizer.Core 3.0.10 output, captured before removing the dependency.
    // This representative corpus includes test and sample methods, acronyms, digits,
    // underscore/PascalCase mixtures and ordinary names. No corpus entries differ.
    [Test]
    [Arguments("TestPass", "Test pass")]
    [Arguments("TestFail", "Test fail")]
    [Arguments("CreateAnAccount", "Create an account")]
    [Arguments("TheHttpStatusCodeIs", "The http status code is")]
    [Arguments("NoScenarioText", "No scenario text")]
    [Arguments("No_Scenario_Text_with_Hyphens", "No Scenario Text with Hyphens")]
    [Arguments("Test1", "Test 1")]
    [Arguments("NotRunTest3", "Not run test 3")]
    [Arguments("More_than_50_entries_are_rejected_even_when_duplicated", "More than 50 entries are rejected even when duplicated")]
    [Arguments("RetryCreatesAndConfiguresANewContextOnce", "Retry creates and configures a new context once")]
    [Arguments("NUNitSuccessException", "NU nit success exception")]
    [Arguments("NUNitIgnoreException", "NU nit ignore exception")]
    [Arguments("CustomHtmlIsIsolatedInAScriptlessFrame", "Custom html is isolated in a scriptless frame")]
    [Arguments("ZZZ_EnsureContextsAreUnique", "ZZZ EnsureContextsAreUnique")]
    [Arguments("Duplicated_values_fit_the_plain_JSON_upload_size", "Duplicated values fit the plain JSON upload size")]
    [Arguments("Unicode_reports_use_the_exact_UTF8_byte_budget", "Unicode reports use the exact UTF8 byte budget")]
    [Arguments("Host_JSON_defaults_do_not_change_report_capacity", "Host JSON defaults do not change report capacity")]
    [Arguments("Invalid_numeric_statuses_fail_with_a_JSON_error", "Invalid numeric statuses fail with a JSON error")]
    [Arguments("Registration_preserves_host_MVC_formatters_and_JSON_options", "Registration preserves host MVC formatters and JSON options")]
    [Arguments("Raw_HTTP_download_preserves_storage_bytes_and_MVC_disposes_stream", "Raw HTTP download preserves storage bytes and MVC disposes stream")]
    [Arguments("MissingRawJsonReportReturns404WithoutHtml", "Missing raw json report returns 404 without html")]
    [Arguments("Upload_and_raw_download_ignore_host_JSON_options", "Upload and raw download ignore host JSON options")]
    [Arguments("NoHooksIsANoOp", "No hooks is a no op")]
    [Arguments("BDTestRetrySetup", "BD test retry setup")]
    [Arguments("BDTestRetryTearDown", "BD test retry tear down")]
    [Arguments("SetUp", "Set up")]
    [Arguments("TearDown", "Tear down")]
    [Arguments("TestWithContext1", "Test with context 1")]
    [Arguments("InterceptConsoleOutput_Synchronous_And_Asynchronous_Steps_Mix1_Called_Asynchronously", "InterceptConsoleOutput Synchronous And Asynchronous Steps Mix1 Called Asynchronously")]
    public async Task Repository_corpus_matches_frozen_Humanizer_output(string name, string humanizerOutput)
    {
        await Assert.That(ScenarioTextHelper.GetScenarioText(null, name)).IsEqualTo(humanizerOutput);
    }

    // Intentional differences from Humanizer.Core 3.0.10, also listed in the migration notes.
    [Test]
    [Arguments("given_a_user_logs_in", "given a user logs in", "Given a user logs in")]
    [Arguments("when_HTTPRequest_arrives", "when HTTPRequest arrives", "When HTTPRequest arrives")]
    [Arguments("__Given__a_user__", "  Given  a user  ", "Given a user")]
    [Arguments("Version42beta", "Version 42beta", "Version 42 beta")]
    [Arguments("<RunAsync>d__12", "<RunAsync>d  12", "<RunAsync>d__12")]
    [Arguments("<Run>b__0_1", "<Run>b  0 1", "<Run>b__0_1")]
    [Arguments("___", "   ", "___")]
    public async Task Documented_differences_from_Humanizer(string name, string humanizerOutput, string expected)
    {
        await Assert.That(expected).IsNotEqualTo(humanizerOutput);
        await Assert.That(NameHumanizer.Humanize(name)).IsEqualTo(expected);
    }

    [Test]
    [Arguments(nameof(user_logs_in), "User logs in", "user logs in")]
    [Arguments(nameof(MFAToken), "MFA token", "mFA token")]
    [Arguments(nameof(Order2IsShipped), "Order 2 is shipped", "order 2 is shipped")]
    [Arguments(nameof(ExplicitText), "Exact ScenarioText", "Exact StepText")]
    public async Task Metadata_and_steps_use_the_same_rules_and_preserve_explicit_text(string name, string scenarioText, string stepText)
    {
        var method = Required(typeof(NameHumanizerTests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static));
        var metadata = TestMethodMetadata.Get(typeof(NameHumanizerTests), method);
        await Assert.That(ScenarioTextHelper.GetScenarioText(metadata, "IgnoredCaller")).IsEqualTo(scenarioText);
        await Assert.That(StepTextHelper.GetStepText(method, Array.Empty<Func<object>>())).IsEqualTo(stepText);
    }

    [Test]
    public async Task Generated_text_is_culture_independent()
    {
        var original = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
            await Assert.That(ScenarioTextHelper.GetScenarioText(null, "itemIsValid")).IsEqualTo("Item is valid");
            await Assert.That(ScenarioTextHelper.GetScenarioText(null, "ItemIsValid")).IsEqualTo("Item is valid");
            var method = Required(typeof(NameHumanizerTests).GetMethod(nameof(ItemIsValid), BindingFlags.NonPublic | BindingFlags.Static));
            await Assert.That(StepTextHelper.GetStepText(method, Array.Empty<Func<object>>())).IsEqualTo("item is valid");
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = original; }
    }

    [Test]
    public async Task Core_does_not_reference_Humanizer_or_export_the_splitter()
    {
        var assembly = typeof(StepTextHelper).Assembly;
        await Assert.That(assembly.GetReferencedAssemblies().Select(name => name.Name)).DoesNotContain("Humanizer");
        await Assert.That(assembly.GetExportedTypes().Select(type => type.Name)).DoesNotContain("NameHumanizer");
    }

    private static void user_logs_in() { }
    private static void MFAToken() { }
    private static void Order2IsShipped() { }
    private static void ItemIsValid() { }

    [ScenarioText("Exact ScenarioText"), StepText("Exact StepText")]
    private static void ExplicitText() { }
}
