namespace BDTest.Tests.Fixtures;

public class SetupTeardownFixture
{
    [After(HookType.Assembly)]
    public static void DeleteOutputDirectories() => FileHelpers.DeleteOutputRoot();
}
