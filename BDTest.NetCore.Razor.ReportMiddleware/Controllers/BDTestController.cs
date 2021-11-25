using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using BDTest.Maps;
using BDTest.NetCore.Razor.ReportMiddleware.Constants;
using BDTest.NetCore.Razor.ReportMiddleware.Extensions;
using BDTest.NetCore.Razor.ReportMiddleware.Helpers;
using BDTest.NetCore.Razor.ReportMiddleware.Interfaces;
using BDTest.NetCore.Razor.ReportMiddleware.Models;
using BDTest.NetCore.Razor.ReportMiddleware.Models.ViewModels;
using BDTest.Test;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace BDTest.NetCore.Razor.ReportMiddleware.Controllers
{
    [Route("bdtest")]
    public class BDTestController : Controller
    {
        private readonly IDataRepository _dataRepository;
        private readonly BDTestReportServerOptions _bdTestReportServerOptions;

        public BDTestController(IDataRepository dataRepository, BDTestReportServerOptions bdTestReportServerOptions)
        {
            _dataRepository = dataRepository;
            _bdTestReportServerOptions = bdTestReportServerOptions;
        }
        
        [HttpGet]
        [AllowAnonymous]
        [Route("ping")]
        public IActionResult Ping()
        {
            return Ok();
        }

        [AllowAnonymous]
        [HttpPost]
        [Route("data")]
        public async Task<IActionResult> Index([FromBody] BDTestOutputModel bdTestOutputModel)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            var id = bdTestOutputModel.Id ?? Guid.NewGuid().ToString("N");

            await _dataRepository.StoreData(bdTestOutputModel, id);

            if (_bdTestReportServerOptions.DataReceiver != null)
            {
                await _bdTestReportServerOptions.DataReceiver.OnReceiveTestDataAsync(bdTestOutputModel);
            }

            return Ok(Url.ActionLink("Summary", "BDTest", new { id }));
        }

        [HttpGet]
        [Route("report/{id}")]
        public IActionResult GetReport([FromRoute] string id)
        {
            return RedirectToAction("Summary", "BDTest", new { id });
        }

        [HttpGet]
        [HttpDelete]
        [Route("report/delete/{id}")]
        public async Task<IActionResult> DeleteReport([FromRoute] string id)
        {
            if (await _bdTestReportServerOptions.AdminAuthorizer.IsAdminAsync(HttpContext) != true)
            {
                return Unauthorized();
            }
            
            await _dataRepository.DeleteReport(id);
            
            return await TestRuns().ConfigureAwait(false);
        }

        [HttpGet]
        [Route("report/{id}/summary")]
        public Task<IActionResult> Summary([FromRoute] string id)
        {
            return GetView(id, model => View("Summary", model));
        }

        [HttpGet]
        [Route("report/{id}/stories")]
        public async Task<IActionResult> Stories([FromRoute] string id)
        {
            var data = await GetData(id).ConfigureAwait(false);

            if (data == null)
            {
                return View("NotFoundSingle", id);
            }
            
            var filterByQueryParameter = Request.GetQueryParameter(StatusConstants.FilterByStatusQueryParameterName);

            var scenariosGroupedByStories = data.Scenarios
                .Where(scenario => scenario != null)
                .GroupBy(scenario => scenario.GetStoryText())
                .OrderBy(group => group.GetTotalStatus().GetOrder())
                .ThenBy(group => group.Key)
                .ToArray();

            if (!string.IsNullOrEmpty(filterByQueryParameter) && filterByQueryParameter != StatusConstants.All)
            {
                scenariosGroupedByStories = scenariosGroupedByStories
                    .Where(group => string.Equals(filterByQueryParameter, group.GetTotalStatus().ToString(), StringComparison.OrdinalIgnoreCase))
                    .ToArray();
            }

            return View("Stories", scenariosGroupedByStories);
        }

        [HttpGet]
        [Route("report/{id}/all-scenarios")]
        public async Task<IActionResult> AllScenarios([FromRoute] string id)
        {
            var data = await GetData(id).ConfigureAwait(false);
            
            if (data == null)
            {
                return View("NotFoundSingle", id);
            }
            
            var filterByQueryParameter = Request.GetQueryParameter(StatusConstants.FilterByStatusQueryParameterName);

            IEnumerable<Scenario> scenarios = data.Scenarios;
            
            if (!string.IsNullOrEmpty(filterByQueryParameter) && filterByQueryParameter != StatusConstants.All)
            {
                scenarios = data.Scenarios
                    .Where(scenario => string.Equals(filterByQueryParameter, scenario.Status.ToString(), StringComparison.OrdinalIgnoreCase));
            }
            
            var orderByQueryParameter = Request.GetQueryParameter(OrderConstants.OrderByQueryParameterName);
            
            var scenariosGroupedByScenarioTextEnumerable = scenarios.GroupBy(scenario => scenario.GetScenarioText())
                .OrderBy(group => group.GetTotalStatus().GetOrder())
                .ThenBy(group => group.Key);
            
            switch (orderByQueryParameter)
            {
                case OrderConstants.Name:
                    scenariosGroupedByScenarioTextEnumerable =
                        scenariosGroupedByScenarioTextEnumerable.OrderBy(scenarios => scenarios.Key);
                    break;
                case OrderConstants.DurationDescending:
                    scenariosGroupedByScenarioTextEnumerable =
                        scenariosGroupedByScenarioTextEnumerable.OrderByDescending(scenarios =>
                            scenarios.Select(scenario => scenario.TimeTaken).Max());
                    break;
                case OrderConstants.DurationAscending:
                    scenariosGroupedByScenarioTextEnumerable =
                        scenariosGroupedByScenarioTextEnumerable.OrderBy(scenarios =>
                            scenarios.Select(scenario => scenario.TimeTaken).Max());
                    break;
                case OrderConstants.DateAscending:
                    scenariosGroupedByScenarioTextEnumerable = scenariosGroupedByScenarioTextEnumerable.OrderBy(scenarios =>
                        BDTestUtil.GetTestTimer(scenarios.ToList()).TestsStartedAt);
                    break;
                case OrderConstants.DateDescending:
                    scenariosGroupedByScenarioTextEnumerable =
                        scenariosGroupedByScenarioTextEnumerable.OrderByDescending(scenarios =>
                            BDTestUtil.GetTestTimer(scenarios.ToList()).TestsStartedAt);
                    break;
                case OrderConstants.Status:
                    // Defaults to status. We don't need to do anything :)
                    break;
                default:
                    _bdTestReportServerOptions.Logger?.LogWarning("Unknown order: {OrderByQueryParameter}", orderByQueryParameter);
                    break;
            }

            return View("AllScenarios", scenariosGroupedByScenarioTextEnumerable.ToArray());
        }
        
        [HttpGet]
        [Route("report/{reportId}/scenario/{scenarioId}")]
        public async Task<IActionResult> SpecificScenario([FromRoute] string reportId, string scenarioId)
        {
            var data = await GetData(reportId).ConfigureAwait(false);

            var scenario = data?.Scenarios?.FirstOrDefault(x => x.Guid == scenarioId);
            
            if (scenario == null)
            {
                return View("NotFoundSingle", reportId);
            }

            return View("Scenario", scenario);
        }

        [HttpGet]
        [Route("report/{id}/timings")]
        public Task<IActionResult> Timings([FromRoute] string id)
        {
            return GetView(id, model => View("TestTimesSummary", model));
        }
        
        [HttpGet]
        [Route("report/{id}/top-defects")]
        public Task<IActionResult> TopDefects([FromRoute] string id)
        {
            return GetView(id, model => View("TopDefects", model));
        }
        
        [HttpGet]
        [Route("report/{id}/warnings")]
        public Task<IActionResult> Warnings([FromRoute] string id)
        {
            return GetView(id, model => View("Warnings", model));
        }

        [HttpGet]
        [Route("/")]
        public IActionResult Redirect()
        {
            return RedirectToAction("TestRuns");
        }

        [HttpGet]
        [Route("report/test-runs")]
        public async Task<IActionResult> TestRuns()
        {
            var records = await _dataRepository.GetAllTestRunRecords();

            return View("TestRunList", records);
        }
        
        [HttpGet]
        [Route("report/trends")]
        public async Task<IActionResult> Trends()
        {
            var records = await _dataRepository.GetAllTestRunRecords();

            return View("Trends", records);
        }

        [HttpGet]
        [Route("report/test-run-times")]
        public async Task<IActionResult> TestRunTimes([FromQuery] string reportIds)
        {
            var reportIdsArray = reportIds?.Split(',') ?? Array.Empty<string>();

            if (!reportIdsArray.Any())
            {
                return RedirectToAction("TestRuns", "BDTest");
            }
            
            var foundReports = (await Task.WhenAll(reportIdsArray.Select(_dataRepository.GetData))).Where(data => data != null).ToList();

            if (!foundReports.Any())
            {
                return View("NotFoundMultiple");
            }

            return View("MultipleTestRunsTimes", foundReports);
        }

        [HttpGet]
        [Route("report/test-run-flakiness")]
        public async Task<IActionResult> TestRunFlakiness()
        {
            var records = await _dataRepository.GetAllTestRunRecords();

            return View("FlakinessTestSelector", records);
        }
        
        [HttpPost]
        [Route("report/test-run-flakiness")]
        public async Task<IActionResult> TestRunFlakiness([FromForm] string reportIds)
        {
            var reportIdsArray = reportIds?.Split(",") ?? Array.Empty<string>();

            if (!reportIdsArray.Any())
            {
                return RedirectToAction("TestRuns", "BDTest");
            }
            
            var foundReports = (await Task.WhenAll(reportIdsArray.Select(_dataRepository.GetData))).Where(data => data != null).ToList();

            if (!foundReports.Any())
            {
                return View("NotFoundMultiple");
            }

            return View("MultipleTestRunsFlakiness", foundReports);
        }

        [HttpGet]
        [Route("report/{id}/raw-json-data")]
        public async Task<IActionResult> RawJsonData([FromRoute] string id)
        {
            var model = await _dataRepository.GetData(id);
            
            if (model == null)
            {
                return View("NotFoundSingle", id);
            }
            
            return Ok(JsonConvert.SerializeObject(model));
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel {RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier});
        }

        private async Task<IActionResult> GetView(string id, Func<BDTestOutputModel, IActionResult> viewAction)
        {
            var model = await _dataRepository.GetData(id);
            
            if (model == null)
            {
                return View("NotFoundSingle", id);
            }

            ViewBag.Id = id;

            return viewAction(model);
        }

        private Task<BDTestOutputModel> GetData(string id)
        {
            ViewBag.Id = id;
            return _dataRepository.GetData(id);
        }
    }
}