using System.Runtime.CompilerServices;
using BDTest.Output;

namespace BDTest;

internal static class Initialiser
{
    private static bool _alreadyRun;

    [MethodImpl(MethodImplOptions.Synchronized)]
    public static void Initialise()
    {
        if (_alreadyRun)
        {
            return;
        }

        _alreadyRun = true;
            
        InternalTestTimeData.TestsStartedAt = DateTime.Now;
    }
}