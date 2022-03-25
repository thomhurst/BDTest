using BDTest.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BDTest.Tests.Helpers;

public class JsonHelper
{
    public static JObject GetTestDynamicJsonObject()
    {
        var jsonText = BDTestJsonHelper.GetTestJsonData();
        return JObject.Load(new JsonTextReader(new StringReader(jsonText)));
    }
}