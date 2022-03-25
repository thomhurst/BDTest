using System.Text;
using Newtonsoft.Json;

namespace BDTest.Example.Helpers;

public class JsonContent : StringContent
{
    public static readonly string ContentType = "application/json";
        
    public JsonContent(string content) : base(content, Encoding.UTF8, ContentType)
    {
    }

    public JsonContent(string content, Encoding encoding) : base(content, encoding, ContentType)
    {
    }
}
    
public class JsonContent<T> : StringContent
{
    public JsonContent(T content) : base(JsonConvert.SerializeObject(content), Encoding.UTF8, JsonContent.ContentType)
    {
    }

    public JsonContent(T content, Encoding encoding) : base(JsonConvert.SerializeObject(content), encoding, JsonContent.ContentType)
    {
    }
}