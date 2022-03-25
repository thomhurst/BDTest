using Newtonsoft.Json;

namespace BDTest.Test;

internal class BDTestRuntimeInformation
{
    [JsonIgnore]
    internal string CallerMember { get; set; }
    [JsonIgnore]
    internal string CallerFile { get; set; }
    [JsonIgnore]
    internal BDTestBase BdTestBase { get; set; }
}