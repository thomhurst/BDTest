using BDTest.Test;

namespace BDTest.MSTest;

/// <summary>Supplies the stable scenario host selected by an MSTest fixture.</summary>
/// <remarks>Use BDTestClass and forward MSTest's TestContext setter to MSTestBDTestLifecycle.Initialize.</remarks>
public interface IBDTestMSTestTest
{
    /// <summary>Gets the host created for this fixture and its owning run.</summary>
    BDTestBase BDTestHost { get; }
}
