using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using BDTest.Helpers;
using BDTest.Razor.Reports.Helpers;

namespace BDTest.ReportGenerator.RazorServer;

public static class Extensions
{
    public static Stream AsStream<T>(this T t)
    {
        var typeInfo = (JsonTypeInfo<T>)(BDTestJsonContext.Default.GetTypeInfo(typeof(T))
            ?? ReportJsonContext.Default.GetTypeInfo(typeof(T))
            ?? throw new NotSupportedException($"No generated report metadata for {typeof(T)}."));
        var stream = new MemoryStream();
        try
        {
            JsonSerializer.Serialize(stream, t, typeInfo);
            stream.Position = 0;
            return stream;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    public static Stream AsStream(this string? value)
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
