using System.Text.Json.Serialization;

namespace BDTest.Test;

public class ExceptionWrapper
{
    [JsonInclude]
    public string Message { get; set; }
        
    [JsonInclude]
    public string StackTrace { get; set; }
        
    [JsonInclude]
    public string TypeName { get; set; }
        
    [JsonInclude]
    public string AsString { get; set; }

    [JsonConstructor]
    internal ExceptionWrapper()
    {
    }

    public ExceptionWrapper(Exception exception)
    {
        TypeName = exception.GetType().Name;
        Message = exception.Message;
        StackTrace = exception.StackTrace;
        AsString = exception.ToString();
    }
}