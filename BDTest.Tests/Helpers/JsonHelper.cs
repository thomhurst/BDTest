using BDTest.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BDTest.Tests.Helpers;

public class JsonHelper
{
    public static JObject GetTestDynamicJsonObject(BDTestRun run)
    {
        var jsonText = BDTestJsonHelper.GetTestJsonData(null, run);
        return JObject.Load(new JsonTextReader(new StringReader(jsonText)));
    }
}