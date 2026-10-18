using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using BDTest.Reporters;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
public class ConsoleReporterTests
{
    [Test]
    public void Core_has_no_references_to_process_wide_console_colour_mutations()
    {
        // Inspect compiled metadata, including async state machines. A temporary
        // colour change followed by ResetColor must fail even on a redirected console.
        using var assembly = File.OpenRead(typeof(ConsoleReporter).Assembly.Location);
        using var pe = new PEReader(assembly);
        var metadata = pe.GetMetadataReader();
        var mutations = metadata.MemberReferences.Select(metadata.GetMemberReference)
            .Where(member => member.Parent.Kind == HandleKind.TypeReference)
            .Where(member =>
            {
                var type = metadata.GetTypeReference((TypeReferenceHandle)member.Parent);
                return metadata.GetString(type.Namespace) == "System"
                    && metadata.GetString(type.Name) == "Console";
            })
            .Select(member => metadata.GetString(member.Name))
            .Where(name => name is "set_ForegroundColor" or "set_BackgroundColor" or "ResetColor")
            .ToArray();

        Assert.That(mutations, Is.Empty, "Reporting must never change process-wide console colours.");
    }

    [Test]
    public async Task Parallel_scenarios_keep_reporter_text_complete_ordered_and_isolated()
    {
        using var run = BDTestRun.Start();
        const int scenarioCount = 8;
        const int lineCount = 20;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = 0;
        var scenarios = await Task.WhenAll(Enumerable.Range(0, scenarioCount).Select(id => Task.Run(async () =>
        {
            var fixture = new Fixture(run);
            return await fixture.When(async () =>
                {
                    if (Interlocked.Increment(ref entered) == scenarioCount) ready.SetResult();
                    await ready.Task.WaitAsync(TimeSpan.FromSeconds(30));
                    for (var line = 0; line < lineCount; line++)
                    {
                        await ConsoleReporter.Write($"scenario-{id}: ");
                        await Task.Yield();
                        await ConsoleReporter.WriteLine($"line-{line}");
                        ConsoleReporter.WriteLineToConsoleOnly($"console-only-{id}-{line}");
                    }
                })
                .Then(() => ConsoleReporter.WriteLine($"done-{id}"))
                .WithScenarioText($"parallel scenario {id}")
                .BDTestAsync();
        })));

        for (var id = 0; id < scenarioCount; id++)
        {
            var expected = string.Join(Environment.NewLine,
                Enumerable.Range(0, lineCount).Select(line => $"scenario-{id}: line-{line}"));
            var scenario = scenarios[id];
            Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
            Assert.That(scenario.Steps[0].Output, Is.EqualTo(expected));
            Assert.That(scenario.Steps[1].Output, Is.EqualTo($"done-{id}"));
            Assert.That(scenario.Output, Is.EqualTo(expected + Environment.NewLine + $"done-{id}"));
        }
        Assert.That(run.Scenarios.Count, Is.EqualTo(scenarioCount));
    }

    private sealed class Fixture(BDTestRun run) : BDTestBuilder
    {
        public override BDTestRun TestRun => run;
    }
}
