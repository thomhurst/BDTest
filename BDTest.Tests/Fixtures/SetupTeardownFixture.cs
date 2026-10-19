using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[SetUpFixture]
public class SetupTeardownFixture
{
    [OneTimeTearDown]
    public void DeleteOutputDirectories() => FileHelpers.DeleteOutputRoot();
}
