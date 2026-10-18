using System.Text.Json.Serialization;

namespace BDTest.Contracts;

/// <summary>A versioned report snapshot, independent of test execution.</summary>
public sealed class BDTestReport
{
    public int SchemaVersion { get; }
    public string Id { get; }
    public string ProducerVersion { get; }
    public IReadOnlyList<ScenarioResult> Scenarios { get; }
    public IReadOnlyList<ScenarioDefinitionSummary> NotRun { get; }
    public string? Environment { get; }
    public string? Tag { get; }
    public string? BranchName { get; }
    public string? MachineName { get; }
    public ReportTimer? TestTimer { get; }
    public IReadOnlyDictionary<string, string> CustomProperties { get; }
    public string? AttachmentUploadTokenHash { get; }

    /// <summary>Changes only when the report JSON contract changes, independently of package versions.</summary>
    public const int CurrentSchemaVersion = 1;

    [JsonConstructor]
    public BDTestReport(
        int schemaVersion,
        string id,
        string producerVersion,
        IReadOnlyList<ScenarioResult>? scenarios = null,
        IReadOnlyList<ScenarioDefinitionSummary>? notRun = null,
        string? environment = null,
        string? tag = null,
        string? branchName = null,
        string? machineName = null,
        ReportTimer? testTimer = null,
        IReadOnlyDictionary<string, string>? customProperties = null,
        string? attachmentUploadTokenHash = null)
    {
        if (schemaVersion != CurrentSchemaVersion)
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), schemaVersion, "Unsupported report schema version.");
        SchemaVersion = schemaVersion;
        Id = id ?? throw new ArgumentNullException(nameof(id));
        ProducerVersion = producerVersion ?? throw new ArgumentNullException(nameof(producerVersion));
        Scenarios = ContractCollections.List(scenarios);
        NotRun = ContractCollections.List(notRun);
        Environment = environment;
        Tag = tag;
        BranchName = branchName;
        MachineName = machineName;
        TestTimer = testTimer;
        CustomProperties = ContractCollections.Dictionary(customProperties);
        AttachmentUploadTokenHash = attachmentUploadTokenHash;
    }
}
