using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BDTest.Tests
{
    public class JsonHelper
    {
        public static JObject GetDynamicJsonObject()
        {
            var jsonText = File.ReadAllText(FileHelpers.GetJsonFilePath());
            return JObject.Load(new JsonTextReader(new StringReader(jsonText)));
        }
    }
}