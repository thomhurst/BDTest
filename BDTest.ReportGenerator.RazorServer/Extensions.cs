using System.Text;
using Newtonsoft.Json;

namespace BDTest.ReportGenerator.RazorServer;

public static class Extensions
{
    public static Stream AsStream<T>(this T t)
    {
        return WriteStream(writer =>
        {
            using var jsonWriter = new JsonTextWriter(writer);
            JsonSerializer.CreateDefault().Serialize(jsonWriter, t);
        });
    }
        
    public static Stream AsStream(this string value)
    {
        return WriteStream(writer => writer.Write(value));
    }

    private static Stream WriteStream(Action<StreamWriter> write)
    {
        var stream = new MemoryStream();
        try
        {
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false, true), 1024, leaveOpen: true))
            {
                write(writer);
            }

            stream.Position = 0;
            return stream;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }
}
