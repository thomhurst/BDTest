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
            
            var guid = Guid.NewGuid().ToString("N");

            _cache.Set(guid, bdTestOutputModel);

            return Redirect($"report/{guid}");
        }

        [HttpGet]
        [Route("report/{id}")]
        public IActionResult GetReport([FromRoute] string id)
        {
            if(_cache.TryGetValue(id, out var model))
            {
                return View("Layout", model);   
            }

            return NotFound();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel {RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier});
        }
    }
}