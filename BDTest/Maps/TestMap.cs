using System;
using System.Collections.Concurrent;
using BDTest.Test;

namespace BDTest.Maps
{
    internal static class TestMap
    {
        internal static ConcurrentDictionary<Guid, BuildableTest> Testables = new ConcurrentDictionary<Guid, BuildableTest>();
    }
}
