using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BDTest.Example.Context;

public class MyApiContext
{
    public HttpClient HttpClient { get; } = new();
    public HttpRequestMessage RequestMessage { get; set; }
    public HttpResponseMessage ResponseMessage { get; private set; }
        
    public async Task ExecuteRequest()
    {
        ResponseMessage = await HttpClient.SendAsync(RequestMessage);
    }
        
    public async Task ExecuteRequest(HttpRequestMessage httpRequestMessage)
    {
        RequestMessage = httpRequestMessage;
        ResponseMessage = await HttpClient.SendAsync(RequestMessage);
    }

    public async Task<T> DeserializeResponse<T>()
    {
        var stringBodyContent = await ResponseMessage.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<T>(stringBodyContent);
    }  
}