using System.Text.Json;

namespace BDTest.Example.Context;

public class MyApiContext
{
    private static readonly JsonSerializerOptions ResponseJsonOptions = new() { PropertyNameCaseInsensitive = true };

    public HttpClient HttpClient { get; } = new();
    public HttpRequestMessage? RequestMessage { get; set; }
    public HttpResponseMessage? ResponseMessage { get; private set; }
        
    public async Task ExecuteRequest()
    {
        var request = RequestMessage ?? throw new InvalidOperationException("Set a request before executing it");
        ResponseMessage = await HttpClient.SendAsync(request);
    }
        
    public async Task ExecuteRequest(HttpRequestMessage httpRequestMessage)
    {
        RequestMessage = httpRequestMessage;
        ResponseMessage = await HttpClient.SendAsync(httpRequestMessage);
    }

    public async Task<T?> DeserializeResponse<T>()
    {
        var response = ResponseMessage ?? throw new InvalidOperationException("Execute a request before reading its response");
        var stringBodyContent = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(stringBodyContent, ResponseJsonOptions);
    }  
}
