using System;
using System.Diagnostics;
using System.Threading.Tasks;
using BDTest.Maps;
using BDTest.ReportGenerator.RazorServer.Interfaces;
using BDTest.ReportGenerator.RazorServer.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace BDTest.ReportGenerator.RazorServer.Controllers
{
    public class HomeController : Controller
    {
        private readonly IMemoryCacheDataStore _memoryCacheDataStore;
        private readonly ILogger<HomeController> _logger;

        public HomeController(IMemoryCacheDataStore memoryCacheDataStore, ILogger<HomeController> logger)
        {
            _memoryCacheDataStore = memoryCacheDataStore;
            _logger = logger;
        }

        [HttpPost]
        [Route("data")]
        public IActionResult Index([FromBody] BDTestOutputModel bdTestOutputModel)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }
            
            var id = Guid.NewGuid().ToString("N");

            _memoryCacheDataStore.StoreData(id, JsonConvert.SerializeObject(bdTestOutputModel));

            return RedirectToAction("Summary", "Home", new { id });
        }

        [HttpGet]
        [Route("report/{id}")]
        public IActionResult GetReport([FromRoute] string id)
        {
            return RedirectToAction("Summary", "Home", new { id });
        }

        [HttpGet]
        [Route("report/{id}/summary")]
        public Task<IActionResult> Summary([FromRoute] string id)
        {
            return GetView(id, "Summary");
        }
        
        [HttpGet]
        [Route("report/{id}/stories")]
        public Task<IActionResult> Stories([FromRoute] string id)
        {
            return GetView(id, "Stories");
        }

        private async Task<IActionResult> GetView(string id, string viewName)
        {
            var model = await _memoryCacheDataStore.GetDataFromStore(id);
            
            if (model == null)
            {
                return NotFound($"Report Not Found: {id}");
            }

            ViewBag.Id = id;
            
            return View(viewName, JsonConvert.DeserializeObject<BDTestOutputModel>(model));

        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel {RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier});
        }
    }
}