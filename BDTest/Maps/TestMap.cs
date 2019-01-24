using System;
using System.Collections.Concurrent;
using BDTest.Test;

namespace BDTest.Maps
{
    internal static class TestMap
    {
        internal static ConcurrentDictionary<Guid, BuildableTest> NotRun = new ConcurrentDictionary<Guid, BuildableTest>();
        internal static ConcurrentDictionary<Guid, Scenario> StoppedEarly = new ConcurrentDictionary<Guid, Scenario>();
    }
}
