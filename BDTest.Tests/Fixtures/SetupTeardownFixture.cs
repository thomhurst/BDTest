using BDTest.Settings;
using BDTest.Tests.Helpers;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[SetUpFixture]
public class SetupTeardownFixture
{
    private string? _environment;
    private string? _tag;

    [OneTimeSetUp]
    public void Settings()
    {
        _environment = LegacySettings.Get<string>("Environment");
        _tag = LegacySettings.Get<string>("Tag");
        LegacySettings.Set("Environment", "Local");
        LegacySettings.Set("Tag", "AcceptanceTests");
    }

    [OneTimeTearDown]
    public void RestoreSettings()
    {
        LegacySettings.Set("Environment", _environment);
        LegacySettings.Set("Tag", _tag);
    }

    [OneTimeTearDown]
    public void DeleteOutputDirectories() => FileHelpers.DeleteOutputRoot();
}
