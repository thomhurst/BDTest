# BDTest
## A testing and reporting framework for .NET

[![nuget](https://img.shields.io/nuget/v/BDTest.svg)](https://www.nuget.org/packages/BDTest/)
[![Codacy Badge](https://api.codacy.com/project/badge/Grade/976b0c6b323b43ef94334f503af9b737)](https://www.codacy.com/app/thomhurst/BDTest?utm_source=github.com&amp;utm_medium=referral&amp;utm_content=thomhurst/BDTest&amp;utm_campaign=Badge_Grade)
[![CodeFactor](https://www.codefactor.io/repository/github/thomhurst/bdtest/badge)](https://www.codefactor.io/repository/github/thomhurst/bdtest)
![Nuget](https://img.shields.io/nuget/dt/BDTest)

### Support

If you like using BDTest, consider buying me a coffee.

<a href="https://www.buymeacoffee.com/tomhurst" target="_blank"><img src="https://www.buymeacoffee.com/assets/img/custom_images/orange_img.png" alt="Buy Me A Coffee" style="height: auto !important;width: auto !important;" ></a>

### Wiki

Please view the [wiki](https://github.com/thomhurst/BDTest/wiki) to see how to use BDTest and how to get setup: https://github.com/thomhurst/BDTest/wiki

### What is it?
BDTest is a testing and reporting framework focusing on 'Behaviour Driven Testing' ideologies. 
It's a way to structure and run your tests so that:
- They are clear, concise and easy to understand
- They are easy to run and debug
- They don't share state. They are independent, side-effect free and able to run in parallel, meaning faster and more stable tests!
- They translate into business criteria and focus on how the system under test would be used by an end user and their behaviours
- You have an easy way to see test results and share them with your team or business

BDTest can be used standalone, or alongside an existing test running framework. For best results, I recommend NUnit, and installing the BDTest.NUnit package alongside the core BDTest package.

Packages are also available for other frameworks: BDTest.xUnit, BDTest.MSTest and BDTest.TUnit. With TUnit, inherit from `TUnitBDTestBase<TContext>`.

#### Why?
I was originally inspired to build BDTest due to SpecFlow.
My team at work used SpecFlow (as does much of the .NET based industry) for their tests when I joined, and upon using it, realised I really didn't enjoy using it and found it to be difficult to work with, finnicky and long-winded.
It turned out, my team also really disliked it, so I thought I'd change things.

Think of BDTest as a pure code-based alternative to SpecFlow.

### What's the benefit over SpecFlow?
- Passing in more complicated arguments to tests. Enums, models, etc.
- You get the benefit of separating steps into Given, When, Then. However you can use and re-use existing steps without redefining or attributing them as Given step, or a When step, etc.
- No auto-generated code. All code is your code! So you shouldn't see any weirdness. This also means no extra weird compilation steps and bringing in specific test compilers/runners.
- Easier to debug. Test has failed and thrown an exception? The stacktrace should take you straight to where it happened. No being taken to strange auto-generated classes that you don't care about!
- Step into all your code. I used Rider as my IDE, which didn't support SpecFlow, so I couldn't step into my test methods. I had to do a painful Find All! Now as it's all code, I can step straight into my test methods.
- No learning and maintaining separate Gherkin files!

### But non-technical team members can write tests using SpecFlow
- Does that EVER actually happen though?
- They can only use the high level methods. Any new methods, a developer will still need to code the steps for them. If they can only use the high level methods, make the method names human readable, and they can still use them!

### I need reports that translates to business acceptance criteria
- BDTest does that!
- BDTest produces you a clear, easy to understand report, showing what failed and what passed, with output and exceptions from tests automatically captured
- Mark your classes and methods with [StoryText], [ScenarioText], and [StepText] attributes, providing the output you want, and your reports will produce clean text output
- If you don't mark your methods with attributes, BDTest will automatically convert the method name to text (separating words by capitalisation or underscores)
- If you use the report server, you can just send the relevant party a URL directly to your test report!

## Where can I use it?
BDTest is written in .NET Standard, so you can use it in either .NET Framework or .NET Core

## How do I get started?

View the [wiki](https://github.com/thomhurst/BDTest/wiki) to see how to use BDTest and how to get setup: https://github.com/thomhurst/BDTest/wiki

### Scenario and story tags

Use `[BDTestTag("smoke")]` from `BDTest.Attributes` on a scenario method or a story
class. Apply the attribute multiple times for multiple tags. Class tags apply to
every scenario in that class, including inherited tags. Method tags combine with
class tags; duplicates are removed ignoring case. Tag names are trimmed and cannot
be empty.

```csharp
[BDTestTag("checkout")]
public class CheckoutTests : BDTestBase
{
    [BDTestTag("smoke")]
    public async Task Successful_checkout()
    {
        // Build and execute your Given/When/Then chain here.
    }
}
```

Tags are available through `BuildableTest.Tags` and `Scenario.Tags` and included in
JSON output, including scenarios that have not run. Existing reports without tags
remain supported. In the report's Scenarios or Stories page, choose a tag and press
Apply to filter scenarios before grouping and pagination. Tag matching ignores
case and combines with the existing status filter. Choose All tags to clear the
tag filter. The report-level `BDTestRunDescriptor.Tag` remains separate from these
scenario tags.

### Execution identity and asynchronous setup

Use a new `BDTestBuilder` for each standalone test. Its execution ID is assigned
when it is constructed, so startup output written from async setup or `Task.Run`
uses the same ID as the scenario. `MSTestBDTestBase<TContext>` also assigns an ID
per fixture instance, keeping async setup, context access, and teardown together.

For a custom adapter inheriting `BDTestBase` or `AbstractContextBDTestBase<TContext>`,
override `BDTestExecutionId` with the runner's stable per-test ID, or call the
protected `MarkTestAsStarted()` synchronously in the flow that runs setup and the
test. Call it before starting any async work, and call `MarkTestAsComplete()` in
teardown. Keep both lifecycle calls inside the same async entry method so its
execution identity does not leak into the caller:

```csharp
public async Task RunTestAsync()
{
    MarkTestAsStarted();
    try
    {
        await SetupAsync();
        await When(() => ActAsync()).Then(() => Verify()).BDTestAsync();
    }
    finally
    {
        MarkTestAsComplete();
    }
}
```

Calling the start hook inside a separate async setup method or `Task.Run` does
not propagate its `AsyncLocal` value back to the caller. The legacy default still
creates an ID on first access for compatibility; custom adapters using async
setup should use the explicit lifecycle or a stable framework ID instead.

### Before and after step hooks

Register asynchronous global hooks during test-run setup:

```csharp
BDTestSettings.BeforeStep += step =>
{
    Console.WriteLine($"Starting: {step.StepText}");
    return Task.CompletedTask;
};

BDTestSettings.AfterStep += async step =>
{
    if (step.Status == Status.Failed)
    {
        await SaveScreenshotAsync(step.ScenarioId, step.Guid);
    }
};
```

Use `BDTest.Settings` for `BDTestSettings` and `BDTest.Test` for `Step` and `Status`.
For per-test hooks, override `OnBeforeStep(Step step)` and `OnAfterStep(Step step)`
on your `BDTestBase` or framework adapter. These methods return `Task` and can
access the test's context. Global handlers may run concurrently for different
tests, so keep their shared state thread-safe. Remove a global handler with `-=`
when its registration is no longer needed.

For each reached step, BDTest awaits global before hooks in registration order,
then the test's before hook, then the step, then the test's after hook, then global
after hooks in registration order. Subscriptions are captured at step entry;
changes during a step apply to subsequent steps. Before hooks stop at the first
exception. Every after hook still runs if a before hook, the step, or another
after hook throws. The original execution exception takes precedence; secondary
after-hook exceptions are logged. Otherwise the first after-hook exception
determines the result using the usual exception and retry rules.

After hooks receive the step's `Status`, `Exception`, `Output`, `StartTime`,
`EndTime`, and `TimeTaken`. Timing includes before hooks and the step, and excludes
after hooks. Output written by hooks is included in the final step output. All
after hooks observe the execution result before after-hook failures are applied.
Hooks run for individually skipped steps and on each retry, but not for steps
unreached after a failure or for a whole scenario skipped before execution.

### Running a scenario once

A runnable Given/When/Then builder supports one execution through `BDTest()`,
`BDTestAsync()`, or direct `await`. Calling any of these again on the same builder,
including while it is running or after it fails, throws
`BDTest.Exceptions.AlreadyExecutedException` before another scenario is created or
any steps run again. Each continuation (such as `Then.And(...)`) creates an independent
builder with its own execution limit, step state, and report entry, even when the
original builder has already run. Create a new chain or continuation to run another scenario. Configured BDTest
retries still run within the original execution and retain one report entry.

`Scenario.Attempts` retains each completed attempt in execution order, including
initial and final attempts. Each entry has a one-based `Number`, `Status`,
`Exception` (type, message, and stack trace), `StartTime`, `EndTime`, and `TimeTaken`.
Durations measure each attempt; retry hooks run between attempts. `Scenario.Status`,
`Steps`, and `TimeTaken` continue to describe the final attempt, and `RetryCount`
continues to count retries. JSON reports include the history; older reports load
with an empty history. Retried scenarios show a **Retry history** section in both
standalone HTML and Razor reports, so failures remain visible after a passing retry.

### But steps

Use `.But(...)` after a Given, When, or Then step to express a contrast. It accepts
synchronous actions and asynchronous tasks, renders the `But` keyword in step text
and reports, and keeps the current phase of the chain:

```csharp
await Given(() => AccountExists())
    .But(() => AccountIsLocked())
    .When(() => SignInAsync())
    .But(() => RetrySignInAsync())
    .Then(() => AccessIsDenied())
    .But(() => AccountStillExists())
    .BDTestAsync();
```

Repeat `.But(...)`, combine it with `.And(...)` in Given and Then phases, or use
`.WithStepText(() => ...)` to customize its text. A But step in the When phase is
skipped when `BDTestSettings.Debug.ShouldSkipWhenStep` is enabled.

### Step text and argument values

Automatically generated step text captures argument values when the step is added
to a builder. BDTest reuses that text during execution, retries, reporting, and
builder continuations. Argument expressions are evaluated once for this initial
text and again when the step itself runs, so keep those expressions free of side
effects. If a value changes during the scenario, use `.WithStepText(() => ...)`
to generate text when the step executes. An empty override falls back to the
cached automatic text.

Delegate arguments are displayed with `ToString()` without invoking the delegate.
Collections display their elements, and dictionaries implementing `IDictionary`
display `key=value` pairs. Registered `IStepTextStringConverter<T>` converters
take precedence over default formatting and accept values assignable to `T`,
including derived classes and interface implementations. The first matching
registered converter is used; within that converter, an exact type match takes
precedence over an assignable match.

### Scenario outlines and example tables

Define one scenario over typed example rows with `ScenarioOutline`:

```csharp
await ScenarioOutline("Adding numbers", new[]
{
    new { Left = 1, Right = 2, Expected = 3 },
    new { Left = 4, Right = 5, Expected = 9 }
}, row => Given(() => SetNumbers(row.Left, row.Right))
    .WithStepText(() => "numbers <Left> and <Right>")
    .When(() => AddNumbers())
    .Then(() => CheckResult(row.Expected)));
```

Public property names become example-table columns. Matching `<Name>` placeholders
in step text (including `[StepText]` and `.WithStepText(...)`) use that row's values,
formatted with invariant culture. Unknown placeholders stay unchanged; null values
display as `null`. Values are captured when the outline is built.

Rows run sequentially with separate steps, output, status, and retry counts. Each
row starts with a fresh `Context`, including `ContextAmendment`; runner setup and
teardown run once for the outline. Put per-example initialization in Given steps.
During context amendment and factory construction, `BDTestContext.CurrentScenario`
is `null` because the example scenario does not exist yet. Steps and retry context
amendments see the executing row.
The factory must return a new chain from the same test instance for each row.
All rows run even when one fails. Afterward, the first exception for the most
severe outcome is rethrown to the test runner. Passed rows with skipped rows pass;
all-skipped rows skip. Failure, not-implemented, and inconclusive rows take priority.

JSON, standalone HTML, and Razor reports contain one outline with an example table
and per-row results. `BDTestUtil.GetScenarios()` returns the outline; its `Examples`
collection contains the row scenarios and their `ExampleValues`. An outline needs
at least one example and, like a regular chain, runs once through `BDTest()`,
`BDTestAsync()`, or direct `await`.

### Asynchronous steps

Asynchronous step expressions must return `Task` (including `Task<T>`). Convert
`ValueTask` and `ValueTask<T>` at the call site so BDTest waits for completion and
reports asynchronous exceptions:

```csharp
await When(() => SaveAsync().AsTask())
    .Then(() => VerifySaved())
    .BDTestAsync();
```

A directly returned `ValueTask`, configured awaitable, or custom awaitable with a
public instance `GetAwaiter()` returning an awaiter that implements `INotifyCompletion`
and exposes a readable Boolean `IsCompleted` and parameterless `GetResult()` is
rejected before the step runs, because binding it
to `Action` would discard its asynchronous work. For other awaitables, write an
`async Task` helper that awaits the operation and use that helper as the step.
Avoid hiding asynchronous work inside a synchronous or `async void` helper.

### Execution and synchronization contexts

`BDTestAsync()` and direct `await` execute steps in order without scheduling a
thread-pool task for each scenario or step. Execution begins inline; when reporting
and all steps complete synchronously, the whole scenario can complete on the calling
thread. Each asynchronous step must finish before the next step starts.

BDTest does not guarantee a synchronization context, thread, or STA apartment across
steps or retries. A step can initially see the caller's synchronization context, but
framework continuations may run without it after an incomplete await. Await the async
entry points instead of blocking on their returned tasks. Code requiring a UI thread
or a particular apartment should explicitly dispatch to that thread.

The synchronous `BDTest()` API remains supported and blocks until completion. It runs
the scenario through one thread-pool boundary so that asynchronous steps do not capture
the blocked caller's synchronization context. It flows the caller's execution context
(including existing `AsyncLocal` values), and preserves original exceptions and output
attribution. A step that explicitly dispatches back to the blocked caller can still
deadlock; prefer `BDTestAsync()` for asynchronous work.

Changes to an `AsyncLocal` value inside a step are not a mechanism for passing data to
later steps or back to the caller. Use the test's `Context` object for shared step data.
The scheduling regression tests cover inline completion, ordered asynchronous steps,
ambient-value isolation, and synchronous execution from a single-thread context. No
throughput or thread-pool-starvation improvement is claimed from these checks.

### Report date formatting

Razor report tables and chart labels use the current request culture's short date
pattern. For example, the same date appears as `17/10/2026` in `en-GB`, `10/17/2026`
in `en-US`, and `17.10.2026` in `de-DE`. Times retain their 24-hour format and up to
three fractional second digits; formatting does not convert time zones.

To select dates using the viewer's browser language, configure ASP.NET Core request
localization in your report host before routing and report endpoints:

```csharp
var reportCultures = new[] { "en-GB", "en-US", "de-DE" };
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture("en-GB")
    .AddSupportedCultures(reportCultures)
    .AddSupportedUICultures(reportCultures));
```

The default localization providers check the culture query parameter, culture cookie,
then the `Accept-Language` header, falling back to the configured default. Hosts can
also customize `DateTimeFormat.ShortDatePattern` on the `CultureInfo` instances they
register in `RequestLocalizationOptions.SupportedCultures`. Without request
localization, the host's current culture determines the date format.

### Report server uploads

Report uploads require an API key. Configure a strong secret on both the server and the uploader before upgrading; an unset or blank key disables uploads (`401`). Existing reports and the ping endpoint remain available.

For an embedded report server:

```csharp
app.UseBDTestReportServer(options =>
{
    options.UploadApiKey = configuration["BDTest:UploadApiKey"];
    options.UploadsPerMinute = 60; // Positive limit shared by authenticated clients of this instance.
});
```

The bundled Razor server reads `BDTest:UploadApiKey` from configuration (for example the `BDTest__UploadApiKey` environment variable). Keep the secret out of source control and use HTTPS. Send it using the existing `HttpClient` overload:

```csharp
using var client = new HttpClient();
client.DefaultRequestHeaders.Add("X-BDTest-Upload-Key", uploadApiKey);
var reportUri = await BDTestReportServer.SendDataAndGetReportUriAsync(
    serverAddress, new BDTestRunDescriptor(), client);
```

`POST /bdtest/data` checks the key and rate limit before parsing JSON. Uploads are limited to 10 MiB per request, including chunked requests, using ASP.NET Core's request size limit (Kestrel/IIS). Configure equivalent or stricter limits in any reverse proxy or alternative host. The default rate limit is 60 authenticated attempts per minute per server instance; excess attempts return `429` with `Retry-After`. The built-in uploader waits for that delay (seconds or an HTTP date) before retrying, with at most three upload attempts. For multiple server instances, enforce a shared limit at the gateway as well.

The server ignores client-supplied report IDs and creates a new GUID for each accepted upload. Use the returned URL to locate the report. Re-uploading (including a retry after a lost response) creates a separate report instead of replacing an existing report. Stored models, summaries, and data-receiver callbacks all receive the server-generated ID.

#### Example Test

```csharp
namespace BDTest.Example
{
    [Story(AsA = "BDTest author",
        IWant = "to show an example of how to use the framework",
        SoThat = "people can get set up easier")]
    public class ExampleTest : NUnitBDTestContext<MyTestContext>
    {
        // Context property is magically available, created by the `NUnitBDTestContext` base class. It's unique for each NUnit test!
        private AccountSteps AccountSteps => new AccountSteps(Context);
        private HttpAssertionsSteps HttpAssertions => new HttpAssertionsSteps(Context);
        
        [Test]
        [ScenarioText("Searching for a non-existing account number returns Not Found")]
        public async Task FindNonExistingAccountReturnsNotFound()
        {
            await When(() => AccountSteps.FindAccount(12345))
                .Then(() => HttpAssertions.TheHttpStatusCodeIs(HttpStatusCode.NotFound))
                .BDTestAsync();
        }
        
        [Test]
        [BugInformation("123456")]
        [ScenarioText("Can successfully create a new account")]
        public async Task CreateNewAccountSuccessfully()
        {
            await When(() => AccountSteps.CreateAnAccount())
                .Then(() => HttpAssertions.TheHttpStatusCodeIs(HttpStatusCode.OK))
                .BDTestAsync();
        }
    }
    
    public class AccountSteps
    {
        private MyTestContext _context;
        
        public class AccountSteps(MyTestContext context)
        {
            _context = context;
        }
        
        [StepText("I create an account")
        public async Task CreateAnAccount()
        {
            // ... Some code to create an account!
            // You can use all the information stored in your test context object that was passed into the constructor!
            // And store stuff for later use - Such as assertions!
            // e.g. 
            // var request = new HttpRequestMessage { Method = POST, Body = SomeBody, Uri = SomeUri };
            // _context.HttpResponse = await HttpHelper.SendAsync(request);
        }
        
        [StepText("I search for the account with the customer ID '{0}'")
        public async Task FindAccount(int customerId)
        {
            // ... Some code to find an account!
            // e.g. 
            // var request = new HttpRequestMessage { Method = GET, Uri = $"{SomeUri}/{customerId}" };
            // _context.HttpResponse = await HttpHelper.SendAsync(request);
        }
    }
    
    public class HttpAssertionsSteps
    {
        private MyTestContext _context;
        
        public class HttpAssertionsSteps(MyTestContext context)
        {
            _context = context;
        }
        
        [StepText("the HTTP status code is {0}")
        public async Task TheHttpStatusCodeIs(HttpStatusCode code)
        {
            // ... Some code to assert - Read data previously stored in your context object!
            // e.g.
            // Assert.That(_context.HttpResponse.StatusCode, Is.EqualTo(code));
        }
    }
}
```

### Hosting the sample server behind a reverse proxy

Report navigation uses local URLs and preserves `Request.PathBase`. Clipboard sharing resolves these URLs against the browser's origin. Hosts embedding the Razor library should configure their own host filtering and forwarded headers middleware.

The sample `BDTest.ReportGenerator.RazorServer` accepts only `localhost`, `127.0.0.1`, and `[::1]` by default. Set `AllowedHosts` to your deployment's semicolon-separated host names (without schemes, ports, or paths), for example `reports.example.com;report-backend.internal`. Include the backend host if your proxy sends it in the original Host header. The same allowlist constrains `X-Forwarded-Host`; avoid `*`.

Forwarded headers run before HTTPS redirection and authentication. Only loopback proxies are trusted by default. For a remote proxy, set its actual connection IP through `ReverseProxy__KnownProxies__0` (and `__1`, etc. for additional trusted addresses). IPv4-mapped IPv6 connections need the mapped address. The default limit is one forwarding hop; more complex chains require explicit `ForwardedHeadersOptions` configuration in `Program.CreateBuilder`.

Configure the proxy to overwrite client-supplied forwarding headers and send `X-Forwarded-For`, `X-Forwarded-Proto`, and `X-Forwarded-Host`. When stripping a public URL prefix such as `/reports`, also send `X-Forwarded-Prefix: /reports`. If the proxy preserves the prefix in the forwarded request path, configure `UsePathBase("/reports")` before routing instead. Include the prefix in the Entra redirect URI, for example `https://reports.example.com/reports/signin-oidc`. Do not enable blanket forwarding with `ASPNETCORE_FORWARDEDHEADERS_ENABLED` or clear the trusted proxy lists.

See [ASP.NET Core proxy configuration](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0) for deployment-specific trust and path handling.

### Securing a report server

`BDTest.Razor.Reports` supplies controllers and views, but does not configure authentication or authorization for your host. Report pages, lists, and `/bdtest/report/{id}/raw-json-data` can contain sensitive test output. Configure your host before exposing it to other users.

The sample `BDTest.ReportGenerator.RazorServer` uses Microsoft Entra ID with `Microsoft.Identity.Web` and requires a signed-in user by default. Register a single-tenant web application, register `https://<your-report-host>/signin-oidc` as its redirect URI, and set these values through environment variables or a secret store:

- `AzureAd__TenantId`: your tenant ID.
- `AzureAd__ClientId`: the application's client ID.
- `AzureAd__ClientSecret`: the application's client secret (or configure a certificate through Microsoft.Identity.Web).
- `AzureStorage__ConnectionString`: the existing report storage connection string.
- `BDTest__UploadApiKey`: a long, randomly generated secret shared only with report-uploading test jobs.

For local development, use `dotnet user-secrets set "<Section>:<Key>" "<value>" --project BDTest.ReportGenerator.RazorServer`. Keep secrets out of committed configuration. Configure access to the enterprise application in Entra ID to restrict who may sign in; by default the server allows every authenticated user accepted by that application to read every report. It does not provide per-report permissions.

The upload endpoint, `POST /bdtest/data`, bypasses interactive sign-in but requires the `X-BDTest-Upload-Key` header in the sample host. Missing, incorrect, or multiple keys return HTTP 401; leaving the server's key empty disables uploads. A reader's sign-in cookie cannot replace the upload key, and an upload key does not grant read access. `/bdtest/ping` remains anonymous for warm-up, as does the library's error action. Static CSS, JavaScript, and images remain public.

Pass a configured `HttpClient` to the existing upload overload:

```csharp
using var client = new HttpClient();
client.DefaultRequestHeaders.Add(
    "X-BDTest-Upload-Key",
    Environment.GetEnvironmentVariable("BDTEST_REPORT_UPLOAD_API_KEY"));

var reportUri = await BDTestReportServer.SendDataAndGetReportUriAsync(
    new Uri("https://your-report-host"),
    new BDTestRunDescriptor(),
    client);
```

For your own ASP.NET Core host, register an authentication provider and a fallback authorization policy. For example, with the `Microsoft.Identity.Web` and `Microsoft.Identity.Web.UI` packages:

```csharp
services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(Configuration.GetSection("AzureAd"));
services.AddAuthorization(options => options.FallbackPolicy = options.DefaultPolicy);
services.AddControllersWithViews()
    .AddMicrosoftIdentityUI()
    .AddBdTestReportControllersAndViews();
services.AddRazorPages();
```

Use the `Microsoft.AspNetCore.Authentication.OpenIdConnect`, `Microsoft.Identity.Web`, `Microsoft.Identity.Web.UI`, and `BDTest.Razor.Reports.Extensions` namespaces. Configure `UseBDTestReportServer` with your data store and `UploadApiKey` as shown above. The request pipeline must select the endpoint before checking its authorization:

```csharp
app.UseHttpsRedirection();
app.UseStaticFiles(); // Only public assets belong in the web root.
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapRazorPages();
});
```

The library marks uploads `[AllowAnonymous]` to bypass interactive sign-in, and its upload authorization filter requires `X-BDTest-Upload-Key` independently. Configure `UploadApiKey` for every host; no separate upload authentication middleware is needed. Use HTTPS for both browser sessions and uploads.

See [Microsoft Identity Web setup](https://github.com/AzureAD/microsoft-identity-web/wiki/web-apps) and [ASP.NET Core authorization policies](https://learn.microsoft.com/aspnet/core/security/authorization/policies) for provider configuration and policy customization.

### Report Server Example Image
![Report Server Example Image](https://github.com/thomhurst/BDTest/blob/master/Images/Test%20Runs%20List.png?raw=true)

### Full-report cache capacity and retention

The Razor report server uses a dedicated bounded cache, separate from the host's
`IMemoryCache`. Configure its policy at startup, before resolving the repository
or serving requests:

```csharp
app.UseBDTestReportServer(options =>
{
    options.ReportCacheSizeLimitBytes = 64L * 1024 * 1024; // Default: 64 MiB
    options.ReportCacheRetention = TimeSpan.FromHours(8); // Default: eight hours
    options.DataStore = persistentStore; // Optional; omit for cache-only hosting
});
```

Both settings must be positive. Each full report is charged its serialized UTF-8
JSON size when cached, preserving shared object references as in report uploads.
Accounting uses compact JSON with fixed settings, independent of the host's
`JsonConvert.DefaultSettings`.
Size measurement streams the JSON without retaining a complete serialized copy.
The sum of those sizes cannot exceed the configured budget.
This is an estimate of retained report size, not a hard limit on process memory:
object overhead, temporary serialization allocations, in-flight responses, and
report summaries are outside this budget. Treat uploaded/cached models as immutable;
changes to a retained model are accounted for when it is next cached.

Uploads and ordinary single-report reads cache reports and restart their retention.
When space is needed, expired and least-recently-used reports are evicted in batches
before the incoming report is admitted. Bulk comparisons can reuse cached reports
but neither populate the cache nor restart retention. The host's unrelated cache
entries need no size metadata and do not compete with full reports.

With a persistent `DataStore`, evicted or expired reports are loaded again on demand.
A report larger than the entire budget bypasses the cache; uploads still persist it
and reads still return it. In cache-only hosting, uploads larger than the budget
return HTTP 413 without adding a summary or notifying the data receiver. Increase
the budget or configure persistent storage to accept them.

Cache-only hosting remains supported, but retention is best effort: eviction,
expiration, or a server restart permanently removes report details. An ordinary
read can extend retention but cannot prevent capacity eviction. Summaries may remain
listed after their details expire or are evicted; those links show a missing report.
Use a persistent store when reports must survive these events.

### Bulk report comparisons

The Razor report server accepts up to 50 comma-separated IDs for `GET /bdtest/report/test-run-times`
and `POST /bdtest/report/test-run-flakiness` (form field `reportIds`). Each ID must be 1-128 ASCII
letters, digits, dots, hyphens or underscores; `.` and `..` are invalid. Generated report GUIDs
are supported. Invalid lists receive HTTP 400 before any reports are read. An empty selection
redirects to the test-run list. Duplicate IDs count toward the limit but are fetched only once.

Comparisons read at most five reports concurrently. The built-in repository reuses cached reports
without adding or refreshing full-report cache entries. Normal single-report reads still cache
results. Custom `IDataRepository` implementations can override `GetDataForBulkRead` to apply the
same cache policy; its default implementation delegates to `GetData` for compatibility.

### Custom HTML in reports

`ScenarioHtmlWriter.Text(...)`, both `Link(...)` overloads, and `Image(...)` HTML-encode
supplied text and attribute values. Pass plain text and URLs, without pre-encoding HTML
entities. The delegate overload of `Link` also treats its result as plain text.

Links and images accept HTTP/HTTPS URLs (case-insensitive schemes), protocol-relative
URLs (`//example.com/...`), and relative paths, queries, or fragments. Links also accept
`mailto:` URLs. Other schemes, including `javascript:`, `vbscript:`, `data:`, `file:`,
and `blob:`, are rejected with `ArgumentException`. URLs must be nonempty and cannot
contain control characters, backslashes, or leading/trailing whitespace; null URLs
throw `ArgumentNullException`. Scheme-like colons before the first `/`, `?`, or `#`
must belong to an allowed scheme; use `./name:part.png` for a relative filename with a colon.
This policy restricts schemes, not destination hosts.

Use `ScenarioHtmlWriter.Custom(html)` explicitly when you need raw HTML. It preserves
your markup. The report server continues to isolate custom output in its sandboxed
iframe; helper URL acceptance does not grant navigation or script permissions to that iframe.

### Skipped scenarios and steps

A global skip-test rule skips the entire scenario. A scenario whose steps are all
skipped is also `Skipped`. A mix of executed and intentionally skipped steps is
`Passed` when the executed steps pass; this includes `Debug.ShouldSkipWhenStep`.
A framework skip exception aborts the scenario without retries, records that step
and remaining unexecuted steps as `Skipped`, and preserves previously passed steps.
Genuine inconclusive exceptions remain `Inconclusive`.

NUnit and TUnit adapters propagate whole-scenario skips to their runners. MSTest
uses `Assert.Inconclusive`, which appears as a skipped/not-executed runner result;
BDTest still records a rule-driven or all-steps skip as `Skipped`. An explicit
MSTest `Assert.Inconclusive` in a step remains `Inconclusive` in BDTest.

The xUnit adapter targets xUnit v2, which has no built-in runtime skip mechanism.
It throws `NotSupportedException` for a whole-scenario skip so the runner cannot
silently report success. Use `[Fact(Skip = "reason")]` or `[Theory(Skip = "reason")]`
for discovery-time skipping. See [xUnit's runtime skip support](https://api.xunit.net/v3/3.2.2/Xunit.Sdk.SkipException.html).

Standalone `BDTestBase`/`BDTestBuilder` callers can still inspect the returned
skipped scenario. Custom adapters can override `OnScenarioSkipped(string reason)`
to invoke their runner's skip mechanism. Register runner skip exception types in
`BDTestSettings.CustomExceptionSettings.SkipExceptionTypes`, keeping genuine
inconclusive exceptions in `InconclusiveExceptionTypes`.

Run `pwsh scripts/Test-SkipOutcomes.ps1` to verify actual NUnit, MSTest and TUnit
runner outcomes as well as BDTest's scenario and step statuses.

### Step and scenario timeouts

Import `BDTest.Test` to use the timeout extensions. Async steps can accept a
`CancellationToken` at every `Given`, `When`, `Then`, `And`, and `But` position:

```csharp
var scenario = await Given(token => PrepareAsync(token))
    .WithStepTimeout(TimeSpan.FromSeconds(5))
    .When(token => SendRequestAsync(token))
    .WithStepTimeout(TimeSpan.FromSeconds(10))
    .Then(() => VerifyResponse())
    .WithScenarioTimeout(TimeSpan.FromSeconds(20))
    .BDTestAsync();
```

`WithStepTimeout` applies to the most recently added step. `WithScenarioTimeout`
sets a shared budget for all steps in each execution attempt. The earlier of the
step and remaining scenario limits wins. Timeouts are optional and must be
positive, up to `Int32.MaxValue` milliseconds. Without either limit, execution is
unchanged and token-aware steps receive `CancellationToken.None`. In token-aware overloads, cancellation
arguments are omitted from generated step text and `[StepText]` placeholders.

When a limit expires, BDTest requests cancellation and throws `TimeoutException`
with the limit, its scope, and the active step text. The step and scenario retain
`Status.Failed` for existing reports, with `TimedOut = true`; the step exception
and console summary identify the timeout. Later steps do not run. User exceptions
and cancellation raised before the limit keep their existing classification.
Configured retries start a fresh scenario budget and use new step tokens.

Timed steps run on the thread pool so the limit also bounds synchronous work and
the synchronous portion of async methods. Cancellation is cooperative: synchronous
work or async work that ignores the token may continue after the timeout, including
during a retry. BDTest observes eventual task failures, and late completion cannot
change the recorded step status. Pass the token to cancellable operations and
avoid retries when unfinished work could affect another attempt. The scenario
budget covers elapsed time across steps, including step hooks. Hooks are awaited
outside the execution limits; before-hook time reduces the remaining scenario
budget when the step body starts. If after hooks exhaust the scenario budget,
the active step fails once those hooks finish, including on the final step. Retry
hooks and initial report formatting run before the scenario budget starts.
