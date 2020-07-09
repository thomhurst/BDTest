using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using BDTest.Maps;
using BDTest.NetCore.Razor.ReportMiddleware.Interfaces;
using BDTest.NetCore.Razor.ReportMiddleware.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BDTest.NetCore.Razor.ReportMiddleware.Controllers
{
    [Route("bdtest")]
    public class BDTestController : Controller
    {
        private readonly IDataController _dataController;

        public BDTestController(IDataController dataController)
        {
            _dataController = dataController;
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

            await _dataController.StoreData(bdTestOutputModel, id);

            return Ok(Url.ActionLink("Summary", "BDTest", new { id }));
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
            var records = await _dataController.GetAllTestRunRecords();

            return View("TestRunList", records.OrderByDescending(record => record.StartedAtDateTime).ToList());
        }
        
        [HttpGet]
        [Route("report/trends")]
        public async Task<IActionResult> Trends()
        {
            var records = await _dataController.GetAllTestRunRecords();

            return View("Trends", records.OrderByDescending(record => record.StartedAtDateTime).ToList());
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
            
            var foundReports = (await Task.WhenAll(reportIdsArray.Select(_dataController.GetData))).ToList();

            if (!foundReports.Any())
            {
                return View("NotFoundMultiple");
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
            
            var foundReports = (await Task.WhenAll(reportIdsArray.Select(_dataController.GetData))).ToList();

            if (!foundReports.Any())
            {
                return View("NotFoundMultiple");
            }

            return View("MultipleTestRunsFlakiness", foundReports);
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel {RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier});
        }

        private async Task<IActionResult> GetView(string id, Func<BDTestOutputModel, IActionResult> viewAction)
        {
            var model = await _dataController.GetData(id);
            
            if (model == null)
            {
                return View("NotFoundSingle", id);
            }

            ViewBag.Id = id;

            return viewAction(model);
        }
    }
}