﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using BDTest.Maps;
using BDTest.ReportGenerator.RazorServer.Extensions;
using BDTest.ReportGenerator.RazorServer.Interfaces;
using BDTest.ReportGenerator.RazorServer.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BDTest.ReportGenerator.RazorServer.Controllers
{
    [Route("bdtest")]
    public class BDTestController : Controller
    {
        private readonly IMemoryCacheBdTestDataStore _memoryCacheBdTestDataStore;
        private readonly ILogger<BDTestController> _logger;
        private readonly IBDTestDataStore _customDatastore;

        public BDTestController(IMemoryCacheBdTestDataStore memoryCacheBdTestDataStore, ILogger<BDTestController> logger, IServiceProvider serviceProvider)
        {
            _memoryCacheBdTestDataStore = memoryCacheBdTestDataStore;
            _logger = logger;
            _customDatastore = serviceProvider.GetService<IBDTestDataStore>();
        }

        [HttpPost]
        [Route("data")]
        public async Task<IActionResult> Index([FromBody] BDTestOutputModel bdTestOutputModel)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }
            
            var id = bdTestOutputModel.Id ?? Guid.NewGuid().ToString("N");

            await StoreData(bdTestOutputModel, id);

            return RedirectToAction("Summary", "BDTest", new { id });
        }

        [HttpGet]
        [Route("report/{id}")]
        public IActionResult GetReport([FromRoute] string id)
        {
            return RedirectToAction("Summary", "BDTest", new { id });
        }

        [HttpGet]
        [Route("report/{id}/summary")]
        public Task<IActionResult> Summary([FromRoute] string id)
        {
            return GetView(id, model => View("Summary", model));
        }

        [HttpGet]
        [Route("report/{id}/stories")]
        public Task<IActionResult> Stories([FromRoute] string id)
        {
            return GetView(id, model => View("Stories", model));
        }

        [HttpGet]
        [Route("report/{id}/all-scenarios")]
        public Task<IActionResult> AllScenarios([FromRoute] string id)
        {
            return GetView(id, model => View("AllScenarios", model));
        }

        [HttpGet]
        [Route("report/{id}/timings")]
        public Task<IActionResult> Timings([FromRoute] string id)
        {
            return GetView(id, model => View("TestTimesSummary", model));
        }

        [HttpGet]
        [Route("/")]
        public IActionResult Redirect()
        {
            return RedirectToAction("TestRuns");
        }

        [HttpGet]
        [Route("report/test-runs")]
        public async Task<IActionResult> TestRuns([FromQuery] string reportIds, [FromQuery] int? daysBackToSearch)
        {
            var records = await GetRunsBetweenTimes(DateTime.Now.Subtract(TimeSpan.FromDays(daysBackToSearch ?? 30)),
                DateTime.Now);

            return View("TestRunList", records.OrderByDescending(record => record.DateTime).ToList());
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
            
            var foundReports = (await Task.WhenAll(reportIdsArray.Select(GetData))).ToList();

            if (!foundReports.Any())
            {
                return NotFound("No reports found");
            }

            return View("MultipleTestRunsTimes", foundReports);
        }
        
        [HttpGet]
        [Route("report/test-run-flakiness")]
        public async Task<IActionResult> TestRunFlakiness([FromQuery] string reportIds)
        {
            var reportIdsArray = reportIds?.Split(',') ?? Array.Empty<string>();

            if (!reportIdsArray.Any())
            {
                return RedirectToAction("TestRuns", "BDTest");
            }
            
            var foundReports = (await Task.WhenAll(reportIdsArray.Select(GetData))).ToList();

            if (!foundReports.Any())
            {
                return NotFound("No reports found");
            }

            return View("MultipleTestRunsFlakiness", foundReports);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel {RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier});
        }

        private async Task<IActionResult> GetView(string id, Func<BDTestOutputModel, IActionResult> viewAction)
        {
            var model = await GetData(id);
            
            if (model == null)
            {
                return NotFound($"Report Not Found: {id}");
            }

            ViewBag.Id = id;

            return viewAction(model);
        }

        private async Task<BDTestOutputModel> GetData(string id)
        {
            // Check if it's already in-memory
            var model = await _memoryCacheBdTestDataStore.GetTestData(id);

            if (model == null && _customDatastore != null)
            {
                // Search the backup persistent storage
                model = await _customDatastore.GetTestData(id);
            }

            if (model == null)
            {
                return null;
            }
            
            // Re-cache it to extend the time
            await _memoryCacheBdTestDataStore.StoreTestData(id, model);

            return model;

        }
        
        private async Task<IEnumerable<TestRunOverview>> GetRunsBetweenTimes(DateTime start, DateTime end)
        {
            // Check if it's already in-memory
            var model = await _memoryCacheBdTestDataStore.GetTestRunRecordsBetweenDateTimes(start, end) ?? Array.Empty<TestRunOverview>();

            if (!model.Any() && _customDatastore != null)
            {
                // Search the backup persistent storage
                model = await _customDatastore.GetTestRunRecordsBetweenDateTimes(start, end);
                
                foreach (var testRunRecord in model ?? Array.Empty<TestRunOverview>())
                {
                    await _memoryCacheBdTestDataStore.StoreTestRunRecord(testRunRecord);   
                }
            }

            return model;
        }

        private async Task StoreData(BDTestOutputModel bdTestOutputModel, string id)
        {
            if (await _memoryCacheBdTestDataStore.GetTestData(id) == null)
            {
                var totalStatus = bdTestOutputModel.Scenarios.GetTotalStatus();
                var currentDateTime = DateTime.Now;
                
                // Save to in-memory cache for 3 hours for quick fetching
                await _memoryCacheBdTestDataStore.StoreTestData(id, bdTestOutputModel);
                await _memoryCacheBdTestDataStore.StoreTestRunRecord(bdTestOutputModel.GetOverview());
                
                if (_customDatastore != null)
                {
                    // Save to persistent storage if it's configured!
                    await _customDatastore.StoreTestData(id, bdTestOutputModel);
                    await _customDatastore.StoreTestRunRecord(bdTestOutputModel.GetOverview());
                }
            }
        }
    }
}