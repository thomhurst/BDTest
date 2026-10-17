namespace BDTest.Test;

// ReSharper disable once UnusedType.Global
public class BDTestBuilder : BDTestBase
{
    // A builder owns one execution, including setup performed in child async flows.
    protected override string BDTestExecutionId { get; } = Guid.NewGuid().ToString("N");
}