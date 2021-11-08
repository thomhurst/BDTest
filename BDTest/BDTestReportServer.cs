using System;
using System.Diagnostics.CodeAnalysis;
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
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    [SuppressMessage("ReSharper", "UnusedType.Global")]
    public static class BDTestReportServer
    {
        public static Task<Uri> SendDataAndGetReportUriAsync(Uri serverAddress)
        {
            var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(2)
            };

            return SendDataAndGetReportUriAsync(serverAddress, httpClient);
        }
        
        public static async Task<Uri> SendDataAndGetReportUriAsync(Uri serverAddress, HttpClient httpClient)
        {
            var pingUri = new UriBuilder(serverAddress)
            {
                Path = "bdtest/ping"
            }.Uri;
            
            try
            {
                await httpClient.GetAsync(pingUri);
            }
            catch
            {
                // Result ignored - This is just to make sure the server is warmed up. If not, it'll warm it up!
            }
            
            var uploadUri = new UriBuilder(serverAddress)
            {
                Path = "bdtest/data"
            }.Uri;

            var scenarios = TestHolder.ScenariosByInternalId.Values.ToList();
            
            var dataOutputModel = new BDTestOutputModel
            {
                Id = TestHolder.CurrentReportId,
                Environment = BDTestSettings.Environment,
                Tag = BDTestSettings.Tag,
                BranchName = BDTestSettings.BranchName,
                MachineName = Environment.MachineName,
                Scenarios = scenarios,
                Version = BDTestVersionHelper.CurrentVersion,
                NotRun = TestHolder.NotRun.Values.ToList(),
                TestTimer = BDTestUtil.GetTestTimer(scenarios)
            };

            AddCustomProperties(dataOutputModel);

            var stringContent = JsonConvert.SerializeObject(dataOutputModel);

            string responseContent;
            var attempts = 0;
            while (true)
            {
                var stringHttpContent  = new StringContent(stringContent, Encoding.UTF8, "application/json");
                
                try
                {
                    var httpRequestMessage = new HttpRequestMessage
                    {
                        RequestUri = uploadUri,
                        Method = HttpMethod.Post,
                        Content = stringHttpContent 
                    };

                    var response = await httpClient.SendAsync(httpRequestMessage);

                    responseContent = await response.EnsureSuccessStatusCode().Content.ReadAsStringAsync();
                    break;
                }
                catch (Exception)
                {
                    attempts++;
                    if (attempts == 3)
                    {
                        throw;
                    }
                }
            }
            
            return new Uri(responseContent);
        }

        private static void AddCustomProperties(BDTestOutputModel dataOutputModel)
        {
            foreach (var customProperty in BDTestSettings.CustomProperties)
            {
                dataOutputModel.CustomProperties.Add(customProperty.Key, customProperty.Value);
            }
        }
    }
}