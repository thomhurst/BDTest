using BDTest.Test;

namespace BDTest.TUnit;

/// <summary>Opts a TUnit test instance into automatic BDTest setup and asynchronous completion.</summary>
public interface IBDTestTUnitTest
{
    /// <summary>Gets the stable host created for this test instance and its selected run.</summary>
    BDTestBase BDTestHost { get; }
}
