using System;
using System.Diagnostics;
using BDTest.Maps;
using BDTest.ReportGenerator.RazorServer.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace BDTest.ReportGenerator.RazorServer.Controllers
{
    public class HomeController : Controller
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<HomeController> _logger;

        public HomeController(IMemoryCache cache, ILogger<HomeController> logger)
        {
            _cache = cache;
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

            _cache.Set(id, bdTestOutputModel);

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
        public IActionResult Summary([FromRoute] string id)
        {
            return GetView(id, "Summary");
        }
        
        [HttpGet]
        [Route("report/{id}/stories")]
        public IActionResult Stories([FromRoute] string id)
        {
            return GetView(id, "Stories");
        }

        private IActionResult GetView(string id, string viewName)
        {
            if (!_cache.TryGetValue(id, out var model))
            {
                return NotFound($"Report Not Found: {id}");
            }
            
            ViewBag.Id = id;
            
            return View(viewName, model);

        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel {RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier});
        }
    }
}