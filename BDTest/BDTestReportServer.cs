using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using BDTest.Helpers;
using BDTest.Maps;
using BDTest.Settings;
using Newtonsoft.Json;

namespace BDTest
{
    public static class BDTestReportServer
    {
        public static async Task<Uri> SendDataAndGetReportUri(Uri serverAddress)
        {
            var httpClient = new HttpClient();
            
            var pingUri = new UriBuilder(serverAddress)
            {
                Path = "bdtest/ping"
            }.Uri;

            // Result ignored - This is just to make sure the server is warmed up. If not, it'll warm it up!
            await httpClient.GetAsync(pingUri);
            
            var uploadUri = new UriBuilder(serverAddress)
            {
                Path = "bdtest/data"
            }.Uri;

            var scenarios = TestHolder.Scenarios.Values.ToList();
            
            var dataOutputModel = new BDTestOutputModel
            {
                Id = TestHolder.InstanceGuid,
                Environment = BDTestSettings.Environment,
                Tag = BDTestSettings.Tag,
                Scenarios = scenarios,
                Version = BDTestVersionHelper.CurrentVersion,
                NotRun = TestHolder.NotRun.Values.ToList(),
                TestTimer = BDTestUtil.GetTestTimer(scenarios)
            };

            var stringContent = JsonConvert.SerializeObject(dataOutputModel);
            
            var httpRequestMessage = new HttpRequestMessage
            {
                RequestUri = uploadUri,
                Method = HttpMethod.Post,
                Content = new StringContent(stringContent, Encoding.UTF8, "application/json")
            };
            
            var response = await httpClient.SendAsync(httpRequestMessage);

            var content = await response.EnsureSuccessStatusCode().Content.ReadAsStringAsync();
            
            return new Uri(content);
        }
    }
}