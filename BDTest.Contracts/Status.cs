namespace BDTest.Contracts;

/// <summary>The execution status, serialized by name in schema-versioned reports.</summary>
public enum Status
{
    NotRun,
    Pending,
    Passed,
    Failed,
    Inconclusive,
    NotImplemented,
    Skipped
}
