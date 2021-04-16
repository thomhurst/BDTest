using System;
using System.Diagnostics;

namespace BDTest.Helpers
{
    public static class StackFramesHelper
    {
        public static StackFrame[] GetStackFrames()
        {
            return new StackTrace().GetFrames() ?? Array.Empty<StackFrame>();
        }
    }
}