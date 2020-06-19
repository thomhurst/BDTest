using System;
using System.Diagnostics;
using System.Threading.Tasks;
using BDTest.Maps;
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

        private async Task StoreData(BDTestOutputModel bdTestOutputModel, string id)
        {
            if (await _memoryCacheBdTestDataStore.GetDataFromStore(id) == null)
            {
                // Save to in-memory cache for 3 hours for quick fetching
                await _memoryCacheBdTestDataStore.StoreData(id, bdTestOutputModel);
                
                if (_customDatastore != null)
                {
                    // Save to persistent storage if it's configured!
                    await _customDatastore.StoreData(id, bdTestOutputModel);
                }
            }
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
        [Route("report/{id}/timings")]
        public Task<IActionResult> Timings([FromRoute] string id)
        {
            return GetView(id, model => View("TestTimesSummary", model));
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
            var model = await _memoryCacheBdTestDataStore.GetDataFromStore(id);

            if (model == null && _customDatastore != null)
            {
                // Search the backup persistent storage
                model = await _customDatastore.GetDataFromStore(id);
            }

            if (model == null)
            {
                return null;
            }
            
            // Re-cache it to extend the time
            await _memoryCacheBdTestDataStore.StoreData(id, model);

            return model;

        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel {RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier});
        }
    }
}