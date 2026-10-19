---
title: Output and custom HTML
description: How BDTest captures console output, records setup and teardown output, and lets tests add links, images and HTML to reports.
---

# Output and custom HTML

## Console output

BDTest captures everything written to `Console` while a step runs and stores it on that step (`Step.Output`) and on the scenario (`Scenario.Output`). Reports show the output next to the step that wrote it. You do not need a special logger: `Console.WriteLine` is enough.

Output is tracked per test, using the framework's test ID, so parallel tests do not mix their output.

:::note xUnit
The xUnit adapter does not write to `ITestOutputHelper`. BDTest captures output by replacing `Console.Out`, and reports it in BDTest reports. Inject `Xunit.ITestOutputHelper` and write to it yourself if you also want the output in xUnit's results.
:::

## Setup and teardown output

Output written outside the steps, for example in setup and teardown hooks, is not attached to a step. To include it in the report, use the base class methods:

```csharp title="Illustrative fragment"
public class OrderTests : TUnitBDTestBase<OrderContext>
{
    [Before(HookType.Test)]
    public void Setup()
    {
        WriteStartupOutput($"Test user: {Context.UserName}");
    }

    [After(HookType.Test)]
    public void Teardown()
    {
        WriteTearDownOutput($"Cleaned up order {Context.OrderId}");
    }
}
```

The text appears on the scenario as `TestStartupInformation` and `TearDownOutput`. With async setup, the output keeps the test's identity as long as it runs in the same test flow; see [Custom adapters](../frameworks/custom-adapters.md#execution-identity).

## Custom HTML in reports

For binary content such as screenshots, logs and HAR files, use [attachments](attachments.md) to capture run-owned files and structured metadata. The report uploader sends them to configured attachment storage, and Razor reports show downloads and safe image previews beneath the owning step or scenario. `HtmlWriter` remains available for custom markup and links to already hosted images.

`ScenarioHtmlWriter` on the base class adds content to the current scenario's report entry. Each access returns a new writer for the current test:

```csharp title="Illustrative fragment"
[Test]
public async Task CreatesAnOrder()
{
    await When(() => Orders.Create())
        .Then(() => Orders.TheOrderExists())
        .BDTestAsync();

    ScenarioHtmlWriter.Link("Open the order in the admin portal", $"https://admin.example.com/orders/{Context.OrderId}");
    ScenarioHtmlWriter.LineBreak();
    ScenarioHtmlWriter.Image("https://screenshots.example.com/orders/latest.png");
}
```

| Method | Writes |
| --- | --- |
| `Text(string value)` | HTML-encoded text. |
| `Link(string text, string url)` | A link that opens in a new tab. The text is HTML-encoded. |
| `Link(Func<string> body, string url)` | A link whose text comes from the delegate. The result is treated as plain text. |
| `Image(string url)` | A lazy-loaded image. |
| `LineBreak()` | A line break. |
| `Custom(string html)` | Raw HTML, unchanged. |

### URL rules

`Link` and `Image` HTML-encode the text and attribute values you supply. Pass plain text and URLs, not pre-encoded entities.

- Allowed: `http:` and `https:` (any case), protocol-relative URLs (`//example.com/...`), and relative paths, queries or fragments. `Link` also allows `mailto:`.
- Rejected with `ArgumentException`: other schemes such as `javascript:`, `vbscript:`, `data:`, `file:` and `blob:`; empty URLs; URLs with control characters, backslashes, or leading or trailing whitespace.
- `null` URLs throw `ArgumentNullException`.
- A colon before the first `/`, `?` or `#` must belong to an allowed scheme. For a relative file name containing a colon, write `./name:part.png`.

The policy restricts schemes, not destination hosts.

### Raw HTML

`Custom(html)` keeps your markup exactly as written. Both the report server and the [standalone HTML report](../reports/html-report.md) render custom output in a sandboxed `iframe` with scripts disabled. Links open in new tabs that keep the sandbox restrictions.
