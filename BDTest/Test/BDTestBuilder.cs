namespace BDTest.Test;

// ReSharper disable once UnusedType.Global
public class BDTestBuilder : BDTestBase
{
    /// <summary>Creates a standalone execution in the supplied, caller-owned run.</summary>
    public BDTestBuilder(BDTestRun run)
    {
        TestRun = run ?? throw new ArgumentNullException(nameof(run));
        run.ThrowIfDisposed();
    }

    /// <summary>Gets the run supplied when this builder was constructed.</summary>
    public override BDTestRun TestRun { get; }

    // A builder owns one execution, including setup performed in child async flows.
    protected override string BDTestExecutionId { get; } = Guid.NewGuid().ToString("N");
}
