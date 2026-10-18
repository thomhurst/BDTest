using BDTest.Settings;

namespace BDTest.Tests.Helpers;

// Compatibility tests exercise the shipped obsolete properties without disabling diagnostics.
internal static class LegacySettings
{
    public static Dictionary<string, string> CustomProperties => Required(Get<Dictionary<string, string>>("CustomProperties"));

    public static T? Get<T>(string name) where T : class =>
        (T?)Required(typeof(BDTestSettings).GetProperty(name)).GetValue(null);

    public static void Set(string name, object? value) =>
        Required(typeof(BDTestSettings).GetProperty(name)).SetValue(null, value);
}
