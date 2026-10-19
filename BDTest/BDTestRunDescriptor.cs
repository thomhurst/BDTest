namespace BDTest;

public class BDTestRunDescriptor
{
    public string? Environment { get; set; }
        
    public string? Tag { get; set; }
        
    public string? BranchName { get; set; }

    /// <summary>Additional metadata copied into the selected report at export time.</summary>
    public Dictionary<string, string> CustomProperties { get; } = new();
}