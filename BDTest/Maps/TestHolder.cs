using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using BDTest.Test;

namespace BDTest.Maps
{
    public static class TestHolder
    {
        internal static ConcurrentDictionary<Guid, BuildableTest> NotRun { get; } = new ConcurrentDictionary<Guid, BuildableTest>();
        internal static ConcurrentDictionary<Guid, Scenario> StoppedEarly { get; } = new ConcurrentDictionary<Guid, Scenario>();
        public static ConcurrentBag<Scenario> Scenarios { get; } = new ConcurrentBag<Scenario>();
        
        private static readonly ConcurrentDictionary<string, List<Action<Scenario>>> ScenarioListeners = new ConcurrentDictionary<string, List<Action<Scenario>>>();

        internal static void ListenForScenario(string testId, Action<Scenario> @delegate)
        {
            var list = ScenarioListeners.GetOrAdd(testId, new List<Action<Scenario>>());
            list.Add(@delegate);
        }

        internal static void AddScenario(Scenario scenario)
        {
            Scenarios.Add(scenario);
            
            if (ScenarioListeners.TryGetValue(scenario.FrameworkTestId, out var delegateList))
            {
                foreach (var @delegate in delegateList.ToList())
                {
                    @delegate(scenario);
                    delegateList.Remove(@delegate);
                }
            }
        }
    }
}
