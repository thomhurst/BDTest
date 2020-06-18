using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using BDTest.Helpers;
using BDTest.Maps;
using BDTest.Output;
using BDTest.Test;
using Newtonsoft.Json;

namespace BDTest
{
    public static class BDTestRazorServer
    {
        public static async Task<string> SendData(Uri serverAddress, bool shouldOpenWebPage)
        {
            serverAddress = new UriBuilder(serverAddress)
            {
                Path = "data"
            }.Uri;

            var httpClient = new HttpClient();

            var scenarios = TestHolder.Scenarios.Values.ToList();
            
            var dataOutputModel = new BDTestOutputModel
            {
                Scenarios = scenarios,
                Version = BDTestVersionHelper.CurrentVersion,
                NotRun = TestHolder.NotRun.Values.ToList(),
                TestTimer = GetTestTimer(scenarios)
            };

            var httpRequestMessage = new HttpRequestMessage
            {
                RequestUri = serverAddress,
                Method = HttpMethod.Post,
                Content = new StringContent(JsonConvert.SerializeObject(dataOutputModel), Encoding.UTF8, "application/json")
            };
            
            var response = await httpClient.SendAsync(httpRequestMessage);

            return await response.EnsureSuccessStatusCode().Content.ReadAsStringAsync();
        }
        
        private static TestTimer GetTestTimer(IReadOnlyCollection<Scenario> scenarios)
        {
            if (scenarios.Count == 0)
            {
                return new TestTimer();
            }

            var testTimer = new TestTimer
            {
                TestsStartedAt = scenarios.GetStartDateTime(),
                TestsFinishedAt = scenarios.GetEndDateTime()
            };

            return testTimer;
        }
        
        private static DateTime GetStartDateTime(this IEnumerable<Scenario> scenarios)
        {
            return scenarios.OrderBy(scenario => scenario.StartTime).First().StartTime;
        }

        private static DateTime GetEndDateTime(this IEnumerable<Scenario> scenarios)
        {
            return scenarios.OrderByDescending(scenario => scenario.EndTime).First().EndTime;
        }
    }
}