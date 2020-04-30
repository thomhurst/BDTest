using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BDTest.Tests.Helpers
{
    public class JsonHelper
    {
        // public static JObject GetDynamicJsonObject()
        // {
        //     var jsonText = File.ReadAllText(FileHelpers.GetJsonFilePath());
        //     return JObject.Load(new JsonTextReader(new StringReader(jsonText)));
        // }
        
        public static JObject GetTestDynamicJsonObject()
        {
            var jsonText = File.ReadAllText(FileHelpers.GetUniqueTestJsonFilePath());
            return JObject.Load(new JsonTextReader(new StringReader(jsonText)));
        }
    }
}