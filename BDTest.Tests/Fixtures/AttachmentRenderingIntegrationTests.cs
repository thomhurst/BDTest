using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using BDTest.Helpers;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Test;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public partial class ReportUploadTests
{
    [Test]
    public async Task Captured_hooks_and_retries_upload_and_render_under_their_owner_with_missing_content()
    {
        using var files = new AttachmentDirectory();
        await using var server = await UploadServer.Start(configureServices: services => services.AddSingleton<IBDTestAttachmentStore>(files.Store));
        server.Client.DefaultRequestHeaders.Add("X-BDTest-Upload-Key", Key);
        var options = new BDTestOptions();
        options.RetryTestRules.Add(_ => true, retryLimit: 1);
        using var run = BDTestRun.Start(options);
        var fixture = new RenderingCapture(run);
        fixture.Capture("setup.txt");
        var attempt = 0;
        var scenario = await fixture.When(() =>
        {
            fixture.Capture("body-" + ++attempt);
            if (attempt == 1) throw new InvalidOperationException("retry");
        }).Then(() => { }).BDTestAsync();
        fixture.Capture("teardown.txt");
        await BDTestReportServer.SendDataAndGetReportUriAsync(Required(server.Client.BaseAddress), null, server.Client, run);
        var report = server.Store.Received.Single();
        var html = await server.Client.GetStringAsync($"bdtest/report/{report.Id}/scenario/{scenario.Guid}");
        var steps = Regex.Matches(html, "<details class=\"expander step-item\".*?</details>", RegexOptions.Singleline);
        Assert.That(steps, Has.Count.EqualTo(2));
        var firstStep = steps[0].Value;
        Assert.That(firstStep, Does.Contain("body-1").And.Contain("body-2").And.Contain("before.txt").And.Contain("after.txt"));
        Assert.That(firstStep, Does.Not.Contain("setup.txt").And.Not.Contain("teardown.txt"));
        foreach (var file in Required(Required(scenario.Steps)[0].Attachments))
        {
            var group = Regex.Match(firstStep, $"<section class=\"attachment-list\"[^>]*data-attempt=\"{file.AttemptNumber}\".*?</section>", RegexOptions.Singleline).Value;
            Assert.That(group, Does.Contain($"data-attachment-id=\"{file.Id}\""));
            using var download = await server.Client.GetAsync($"bdtest/report/{report.Id}/attachments/{file.Id}");
            Assert.That(download.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await download.Content.ReadAsStringAsync(), Is.EqualTo(file.Name));
        }
        Assert.That(html, Does.Contain("setup.txt").And.Contain("teardown.txt"));
        Assert.That(steps[1].Value, Does.Not.Contain("body-1").And.Not.Contain("body-2"));

        // Rendering uses metadata, so retention or an interrupted upload cannot break the page.
        await files.Store.DeleteReportAsync(Required(report.Id));
        var attachment = BDTestAttachmentHelper.GetAttachments(report).First();
        using var missing = await server.Client.GetAsync($"bdtest/report/{report.Id}/attachments/{attachment.Id}");
        Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var afterDeletion = await server.Client.GetStringAsync($"bdtest/report/{report.Id}/scenario/{scenario.Guid}");
        Assert.That(afterDeletion, Does.Contain($"data-attachment-id=\"{attachment.Id}\""));
        using var retained = run.OpenAttachment(Required(attachment.Id));
        Assert.That(retained.CanRead, Is.True, "Uploading or deleting server content must not dispose run-owned files.");
    }

    private sealed class RenderingCapture(BDTestRun run) : AbstractContextBDTestBase<object>
    {
        public override BDTestRun TestRun => run;
        protected override string BDTestExecutionId => "attachment-rendering";
        public void Capture(string name) => Attachments.Attach(name, Encoding.UTF8.GetBytes(name), "text/plain");
        public override Task OnBeforeStep(Step step) { Capture("before.txt"); return Task.CompletedTask; }
        public override Task OnAfterStep(Step step) { Capture("after.txt"); return Task.CompletedTask; }
    }
}
