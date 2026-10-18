using System.Text.Json.Serialization;

namespace BDTest.Test;

internal class BDTestRuntimeInformation
{
    [JsonIgnore]
    internal string CallerMember { get; set; }
    [JsonIgnore]
    internal string CallerFile { get; set; }
    [JsonIgnore]
    internal BDTestBase BdTestBase
    {
        get => _bdTestBase ?? (_completedBdTestBase != null && _completedBdTestBase.TryGetTarget(out var target) ? target : null);
        set => _bdTestBase = value;
    }

    private BDTestBase _bdTestBase;
    private WeakReference<BDTestBase> _completedBdTestBase;

    internal void ReleaseRuntimeReferences()
    {
        if (_bdTestBase != null)
        {
            // Preserve access for callers that still own the fixture, without
            // making report history extend the fixture/context lifetime.
            _completedBdTestBase = new WeakReference<BDTestBase>(_bdTestBase);
            _bdTestBase = null;
        }
    }
}