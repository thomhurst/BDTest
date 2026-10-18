using System.Diagnostics;
using BDTest.Maps;
using BDTest.Razor.Reports.Constants;
using BDTest.Razor.Reports.Extensions;
using BDTest.Razor.Reports.Helpers;
using BDTest.Razor.Reports.Implementations;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;
using BDTest.Razor.Reports.Models.ViewModels;
using BDTest.Test;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using BDTest.Helpers;

namespace BDTest.Razor.Reports.Controllers;

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

    // Upload clients authenticate with the upload key, independently of the host's interactive login.
    [AllowAnonymous]
    [HttpPost]
    [Route("data")]
    [ServiceFilter(typeof(ReportUploadFilter))]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Index([ModelBinder(BinderType = typeof(ReportModelBinder))] BDTestOutputModel bdTestOutputModel)
    {
        if (!ModelState.IsValid || bdTestOutputModel == null)
        {
            return BadRequest();
        }

        // Never let an uploader choose a storage key, including after cache expiry.
        var id = Guid.NewGuid().ToString("N");
        bdTestOutputModel.Id = id;

        try
        {
            await _dataRepository.StoreData(id, bdTestOutputModel, HttpContext.RequestAborted);
        }
        catch (ReportTooLargeForCacheException exception)
        {
            return StatusCode(413, exception.Message);
        }

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

    [HttpPost]
    [HttpDelete]
    [ValidateAntiForgeryToken]
    [Route("report/delete/{id}")]
    public async Task<IActionResult> DeleteReport([FromRoute] string id)
    {
        if (await _bdTestReportServerOptions.AdminAuthorizer.IsAdminAsync(HttpContext) != true)
        {
            return Unauthorized();
        }
            
        await _dataRepository.DeleteReport(id, HttpContext.RequestAborted);
            
        return RedirectToAction(nameof(TestRuns));
    }

    [HttpGet]
    [Route("report/{id}/summary")]
    public Task<IActionResult> Summary([FromRoute] string id, CancellationToken cancellationToken)
    {
        return GetView(id, model => View("Summary", model), cancellationToken);
    }

    [HttpGet]
    [Route("report/{id}/stories")]
    public async Task<IActionResult> Stories([FromRoute] string id, CancellationToken cancellationToken)
    {
        var data = await GetData(id, cancellationToken);

        if (data == null)
        {
            return View("NotFoundSingle", id);
        }
            
        var statuses = GetStatusFilter();

        var scenariosGroupedByStories = FilterBySearch(FilterByTag(data.Scenarios))
            .GroupBy(scenario => scenario.GetStoryText())
            .OrderBy(group => group.GetTotalStatus().GetOrder())
            .ThenBy(group => group.Key)
            .ToArray();

        if (statuses != null)
        {
            scenariosGroupedByStories = scenariosGroupedByStories
                .Where(group => statuses.Contains(group.GetTotalStatus()))
                .ToArray();
        }

        return View("Stories", scenariosGroupedByStories);
    }

    [HttpGet]
    [Route("report/{id}/all-scenarios")]
    public async Task<IActionResult> AllScenarios([FromRoute] string id, CancellationToken cancellationToken)
    {
        var data = await GetData(id, cancellationToken);
            
        if (data == null)
        {
            return View("NotFoundSingle", id);
        }
            
        var statuses = GetStatusFilter();

        var scenarios = FilterBySearch(FilterByTag(data.Scenarios));

        if (statuses != null)
        {
            scenarios = scenarios.Where(scenario => statuses.Contains(scenario.Status));
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
            case null:
            case "":
            case OrderConstants.Status:
                // Defaults to status. We don't need to do anything :)
                break;
            default:
                _bdTestReportServerOptions.Logger?.LogWarning("Unknown order: {OrderByQueryParameter}", orderByQueryParameter);
                break;
        }

        return View("AllScenarios", scenariosGroupedByScenarioTextEnumerable.ToArray());
    }

    private IEnumerable<Scenario> FilterByTag(IEnumerable<Scenario> scenarios)
    {
        var tag = Request.GetQueryParameter(TagConstants.FilterByTagQueryParameterName);
        return scenarios.Where(scenario => scenario != null && (string.IsNullOrEmpty(tag)
            || scenario.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)));
    }

    private IEnumerable<Scenario> FilterBySearch(IEnumerable<Scenario> scenarios)
    {
        var search = Request.GetQueryParameter(ScenarioSearch.QueryParameterName)?.Trim();
        return string.IsNullOrEmpty(search) ? scenarios : scenarios.Where(scenario => ScenarioSearch.Matches(scenario, search));
    }

    // filterByStatus accepts one status or a comma-separated list. Null means every status.
    private HashSet<Status> GetStatusFilter()
    {
        var value = Request.GetQueryParameter(StatusConstants.FilterByStatusQueryParameterName);
        if (string.IsNullOrEmpty(value) || string.Equals(value, StatusConstants.All, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // An unknown value matches nothing, as it did before lists were supported.
        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => Enum.TryParse<Status>(part, true, out var status) && Enum.IsDefined(status) ? (Status?)status : null)
            .Where(status => status != null)
            .Select(status => status.Value)
            .ToHashSet();
    }
        
    [HttpGet]
    [Route("report/{reportId}/scenario/{scenarioId}")]
    public async Task<IActionResult> SpecificScenario([FromRoute] string reportId, string scenarioId,
        CancellationToken cancellationToken)
    {
        var data = await GetData(reportId, cancellationToken);

        var scenario = data?.Scenarios?.FirstOrDefault(x => x.Guid == scenarioId);
            
        if (data == null)
        {
            return View("NotFoundSingle", reportId);
        }

        if (scenario == null)
        {
            return View("ScenarioNotFound", data);
        }

        return View("Scenario", scenario);
    }

    [HttpGet]
    [Route("report/{id}/timings")]
    public Task<IActionResult> Timings([FromRoute] string id, CancellationToken cancellationToken)
    {
        return GetView(id, model => View("TestTimesSummary", model), cancellationToken);
    }
        
    [HttpGet]
    [Route("report/{id}/top-defects")]
    [Route("report/{id}/failures")]
    public Task<IActionResult> TopDefects([FromRoute] string id, CancellationToken cancellationToken)
    {
        return GetView(id, model => View("TopDefects", model), cancellationToken);
    }
        
    [HttpGet]
    [Route("report/{id}/warnings")]
    [Route("report/{id}/not-run")]
    public Task<IActionResult> Warnings([FromRoute] string id, CancellationToken cancellationToken)
    {
        return GetView(id, model => View("Warnings", model), cancellationToken);
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
        var records = await _dataRepository.GetAllTestRunRecords(HttpContext.RequestAborted);

        return View("TestRunList", records);
    }
        
    [HttpGet]
    [Route("report/history")]
    [Route("report/trends")]
    public async Task<IActionResult> Trends()
    {
        return View("Trends", await GetHistory(HistoryViewModel.Trends));
    }

    [HttpGet]
    [Route("report/history/timing")]
    public async Task<IActionResult> TimingVariance(CancellationToken cancellationToken)
    {
        return await GetLatestReportsView(HistoryViewModel.Timing, "MultipleTestRunsTimes", cancellationToken);
    }

    [HttpGet]
    [Route("report/test-run-times")]
    public async Task<IActionResult> TestRunTimes([FromQuery] string reportIds, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(reportIds))
        {
            return RedirectToAction(nameof(TimingVariance));
        }

        return await GetBulkReportsView(reportIds, "MultipleTestRunsTimes", HistoryViewModel.Timing, cancellationToken);
    }

    [HttpGet]
    [Route("report/history/flaky")]
    [Route("report/test-run-flakiness")]
    public async Task<IActionResult> TestRunFlakiness(CancellationToken cancellationToken)
    {
        return await GetLatestReportsView(HistoryViewModel.Flaky, "MultipleTestRunsFlakiness", cancellationToken);
    }

    [HttpPost]
    [Route("report/test-run-flakiness")]
    public async Task<IActionResult> TestRunFlakiness([FromForm] string reportIds, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(reportIds))
        {
            return RedirectToAction(nameof(TestRunFlakiness));
        }

        return await GetBulkReportsView(reportIds, "MultipleTestRunsFlakiness", HistoryViewModel.Flaky, cancellationToken);
    }

    private const int MaximumBulkReports = 50;
    private const int MaximumReportIdLength = 128;

    private async Task<HistoryViewModel> GetHistory(string tab)
    {
        var allRuns = (await _dataRepository.GetAllTestRunRecords() ?? Enumerable.Empty<TestRunSummary>())
            .Where(run => run != null)
            .ToList();
        var query = HttpContext?.Request.Query;
        var filter = query == null ? new RunFilter() : RunFilter.FromQuery(query, RunFilter.Last30Days);
        var history = new HistoryViewModel
        {
            Tab = tab,
            Filter = filter,
            AllRuns = allRuns,
            Runs = filter.Apply(allRuns).OrderBy(run => run.StartedAtDateTime).ToList()
        };

        if (query != null && int.TryParse(query["compare"], out var compare))
        {
            history.Compare = Math.Clamp(compare, 2, MaximumBulkReports);
        }

        ViewData[nameof(HistoryViewModel)] = history;
        return history;
    }

    // Compares the latest runs that match the History filters.
    private async Task<IActionResult> GetLatestReportsView(string tab, string viewName, CancellationToken cancellationToken)
    {
        var history = await GetHistory(tab);
        var ids = history.Runs.TakeLast(history.Compare)
            .Select(run => run.RecordId)
            .Where(IsValidReportId);

        history.Reports = await ReadReports(ids, cancellationToken);
        return View(viewName, history.Reports.ToList());
    }

    private static bool IsValidReportId(string id) => !string.IsNullOrEmpty(id) && id.Length <= MaximumReportIdLength
        && id is not "." and not ".." && id.All(character => char.IsAsciiLetterOrDigit(character)
            || character == '-' || character == '_' || character == '.');

    private async Task<List<BDTestOutputModel>> ReadReports(IEnumerable<string> ids, CancellationToken cancellationToken)
    {
        var foundReports = new List<BDTestOutputModel>();
        foreach (var batch in ids.Distinct(StringComparer.Ordinal).Chunk(5))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var reports = await Task.WhenAll(batch.Select(id => _dataRepository.GetDataForBulkRead(id, cancellationToken)));
            foundReports.AddRange(reports.Where(report => report != null));
        }

        return foundReports;
    }

    private async Task<IActionResult> GetBulkReportsView(string reportIds, string viewName, string tab, CancellationToken cancellationToken)
    {
        const int maximumReports = MaximumBulkReports;
        const int maximumIdLength = MaximumReportIdLength;

        // Bound allocation before splitting, and validate the entire request before any storage reads.
        if (reportIds.Length > maximumReports * (maximumIdLength + 1) - 1)
        {
            return BadRequest("Select at most 50 reports with IDs of at most 128 characters.");
        }

        var ids = reportIds.Split(',');
        if (ids.Length > maximumReports || ids.Any(id => id.Length == 0 || id.Length > maximumIdLength
                || id is "." or ".." || id.Any(character => !char.IsAsciiLetterOrDigit(character)
                    && character != '-' && character != '_' && character != '.')))
        {
            return BadRequest("Select at most 50 reports. IDs must contain 1-128 letters, digits, dots, hyphens or underscores, and cannot be '.' or '..'.");
        }

        var foundReports = await ReadReports(ids, cancellationToken);
        if (foundReports.Count == 0)
        {
            return View("NotFoundMultiple");
        }

        var history = await GetHistory(tab);
        history.ChosenRuns = true;
        history.Reports = foundReports;
        return View(viewName, foundReports);
    }

    [HttpGet]
    [Route("report/{id}/raw-json-data")]
    public async Task<IActionResult> RawJsonData([FromRoute] string id, CancellationToken cancellationToken)
    {
        var stream = await _dataRepository.OpenReadAsync(id, cancellationToken);
        if (stream == null) return NotFound();
        // MVC owns and disposes the stream after copying it to the response.
        return File(stream, "application/json");
    }

    [AllowAnonymous]
    // Exception handling re-executes the original HTTP method, including upload POSTs.
    [Route("error")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel {RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier});
    }

    private async Task<IActionResult> GetView(string id, Func<BDTestOutputModel, IActionResult> viewAction, CancellationToken cancellationToken)
    {
        var model = await GetData(id, cancellationToken);

        if (model == null)
        {
            return View("NotFoundSingle", id);
        }

        return viewAction(model);
    }

    private async Task<BDTestOutputModel> GetData(string id, CancellationToken cancellationToken)
    {
        ViewBag.Id = id;
        var model = await _dataRepository.GetData(id, cancellationToken);
        ViewData[nameof(ReportLayoutViewModel)] = new ReportLayoutViewModel { Report = model };
        return model;
    }
}
