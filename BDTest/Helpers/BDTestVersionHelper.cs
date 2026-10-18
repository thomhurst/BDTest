using System.Reflection;

namespace BDTest.Helpers;

public static class BDTestVersionHelper
{
    public static readonly string CurrentVersion = typeof(BDTestVersionHelper).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(BDTestVersionHelper).Assembly.GetName().Version.ToString();
}
