using System.Reflection;
using BDTest.Test;

namespace BDTest.Tests.Helpers;

internal static class LegacyLifecycle
{
    // Standalone fixtures intentionally cover completion without a preceding BeginTest.
    public static void Complete(BDTestBase fixture) =>
        Required(typeof(BDTestBase).GetMethod("MarkTestAsComplete", BindingFlags.Instance | BindingFlags.NonPublic))
            .CreateDelegate<Action>(fixture)();
}
