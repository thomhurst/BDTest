using System.IO;
using Newtonsoft.Json;

namespace BDTest.ReportGenerator.RazorServer;

public static class Extensions
{
    public static Stream AsStream<T>(this T t)
    {
        return JsonConvert.SerializeObject(t).AsStream();
    }
        
    public static Stream AsStream(this string value)
    {
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        writer.Write(value);
        writer.Flush();
        stream.Position = 0;
        return stream;
    }
}