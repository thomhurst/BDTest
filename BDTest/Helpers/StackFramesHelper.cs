using System.Diagnostics;
using System.Reflection;

namespace BDTest.Helpers;

public static class StackFramesHelper
{
    public static StackFrame[] GetStackFrames()
    {
        return new StackTrace().GetFrames() ?? Array.Empty<StackFrame>();
    }

    internal static IEnumerable<MethodBase> GetMethods(IEnumerable<StackFrame> stackFrames)
    {
        foreach (var frame in stackFrames)
        {
            var method = frame.GetMethod();
            if (method != null)
            {
                yield return method;
            }
        }
    }
}
