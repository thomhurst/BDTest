using BDTest.Settings;
using BDTest.Tests.Helpers;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[SetUpFixture]
public class SetupTeardownFixture
{
    private string _environment;
    private string _tag;

    [OneTimeSetUp]
    public void Settings()
    {
        _environment = BDTestSettings.Environment;
        _tag = BDTestSettings.Tag;
        BDTestSettings.Environment = "Local";
        BDTestSettings.Tag = "AcceptanceTests";
    }

    [OneTimeTearDown]
    public void RestoreSettings()
    {
        BDTestSettings.Environment = _environment;
        BDTestSettings.Tag = _tag;
    }

    [OneTimeTearDown]
    public void DeleteOutputDirectories() => FileHelpers.DeleteOutputRoot();
}
