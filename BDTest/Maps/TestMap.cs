using System;
using System.Collections.Concurrent;
using BDTest.Test;

namespace BDTest.Maps
{
    internal static class TestMap
    {
        internal static ConcurrentDictionary<Guid, BuildableTest> NotRun { get; } = new ConcurrentDictionary<Guid, BuildableTest>();
        internal static ConcurrentDictionary<Guid, Scenario> StoppedEarly { get; } = new ConcurrentDictionary<Guid, Scenario>();
        
        internal static readonly ConcurrentDictionary<Guid, Scenario> Scenarios = new ConcurrentDictionary<Guid, Scenario>();
    }
}
