using System.Collections;
using System.Reflection;
using BDTest.Helpers;
using BDTest.Maps;
using BDTest.Razor.Reports.Models;
using BDTest.Settings;
using BDTest.Test;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

public class StructuredExceptionTests
{
    [Test]
    public async Task Nested_and_aggregate_exceptions_round_trip_with_diagnostics()
    {
        var cause = new InvalidOperationException("<actual>") { Source = "<source>" };
        cause.Data["<expected>"] = 42;
        cause.Data["null"] = null;
        var exception = new TargetInvocationException(new AggregateException("aggregate", cause, new ArgumentException("other")));
        var wrapper = new ExceptionWrapper(exception);
        var report = new BDTestOutputModel { Scenarios = [new Scenario { ExecutionException = wrapper }] };

        var json = BDTestJsonHelper.Serialize(report);
        var restored = Required(Required(Required(BDTestJsonHelper.Deserialize(json)).Scenarios).Single().Exception);

        await Assert.That(restored.TypeName).IsEqualTo(typeof(TargetInvocationException).FullName);
        await Assert.That(restored.ShortTypeName).IsEqualTo(nameof(TargetInvocationException));
        await Assert.That(restored.IsWrapper).IsTrue();
        await Assert.That(restored.AsString).IsEqualTo(exception.ToString());
        var children = Required(Required(restored.InnerExceptions).Single().InnerExceptions);
        await Assert.That(children).Count().IsEqualTo(2);
        await Assert.That(Required(children[0].Data)["<expected>"]).IsEqualTo("42");
        await Assert.That(Required(children[0].Data)["null"]).IsEqualTo("");
        await Assert.That(children[0].Source).IsEqualTo("<source>");
        await Assert.That(children[0].HResult).IsEqualTo(cause.HResult);
        await Assert.That(restored.GetRootCauses().Select(item => item.TypeName)).IsEquivalentTo(new[] { typeof(InvalidOperationException).FullName, typeof(ArgumentException).FullName }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(BDTestJsonHelper.Serialize(Required(BDTestJsonHelper.Deserialize(json)))).IsEqualTo(json);
    }

    [Test]
    public async Task Cycles_are_recorded_without_calling_recursive_ToString()
    {
        var exception = new Exception("cycle");
        typeof(Exception).GetField("_innerException", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(exception, exception);
        exception.Data["self"] = exception;

        var wrapper = new ExceptionWrapper(exception);

        await Assert.That(Required(wrapper.InnerExceptions).Single().TruncationReason).IsEqualTo("Cycle");
        await Assert.That(wrapper.AsString).Contains("cycle");
        await Assert.That(Required(wrapper.Data)["self"]).IsEqualTo(typeof(Exception).FullName);
        await Assert.That(wrapper.GetRootCauses()).Count().IsEqualTo(1);
        await Assert.That(() => ReportJson.SerializeObject(wrapper)).ThrowsNothing();
    }

    [Test]
    public async Task Deep_aggregate_messages_do_not_traverse_beyond_the_depth_limit()
    {
        Exception exception = new Exception("deepest");
        for (var index = 0; index < 1000; index++) exception = new AggregateException("aggregate " + index, exception);

        var wrapper = new ExceptionWrapper(exception);

        await Assert.That(wrapper.Message).Contains("truncated");
        await Assert.That(wrapper.GetRootCauses().Single().TruncationReason).IsEqualTo("DepthLimit");
        await Assert.That(() => ReportJson.SerializeObject(wrapper)).ThrowsNothing();
    }

    [Test]
    public async Task Repeated_aggregate_children_are_preserved_and_not_treated_as_cycles()
    {
        var cause = new InvalidOperationException("repeated");
        var wrapper = new ExceptionWrapper(new AggregateException(cause, cause));

        await Assert.That(wrapper.InnerExceptions).Count().IsEqualTo(2);
        await Assert.That(Required(wrapper.InnerExceptions).All(child => child.TruncationReason == null)).IsTrue();
        await Assert.That(wrapper.GetRootCauses()).Count().IsEqualTo(2);
        var groups = FailureGroup.Build([FailedScenario(wrapper)], FailureGroup.ByExceptionType);
        await Assert.That(groups.Single().Scenarios).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Data_preserves_string_key_collisions_and_survives_throwing_formatters()
    {
        var exception = new Exception("data");
        exception.Data[1] = "first";
        exception.Data["1"] = "second";
        exception.Data["bad"] = new BadValue();

        var wrapper = new ExceptionWrapper(exception);


        await Assert.That(Required(wrapper.Data).Values).IsEquivalentTo(new[] { "first", "second", "[Value could not be formatted]" }, global::TUnit.Assertions.Enums.CollectionOrdering.Any);
    }

    [Test]
    [Arguments("getter")]
    [Arguments("null")]
    [Arguments("enumerator")]
    [Arguments("move")]
    [Arguments("entry")]
    [Arguments("source")]
    public async Task Unreadable_diagnostics_do_not_replace_the_original_failure(string failure)
    {
        Exception exception = failure == "source" ? new UnreadableSourceException() : new UnreadableDataException(failure);

        var wrapper = new ExceptionWrapper(exception);

        await Assert.That(wrapper.TypeName).IsEqualTo(exception.GetType().FullName);
        await Assert.That(wrapper.Message).IsEqualTo("original failure");
        await Assert.That(Required(wrapper.InnerExceptions).Single().Message).IsEqualTo("original cause");
        await Assert.That(wrapper.Data).IsEmpty();
        if (failure == "source") await Assert.That(wrapper.Source).IsNull();
        await Assert.That(wrapper.AsString).Contains("original failure");
        await Assert.That(() => ReportJson.SerializeObject(wrapper)).ThrowsNothing();
    }

    [Test]
    [Arguments("getter")]
    [Arguments("null")]
    [Arguments("enumerator")]
    [Arguments("move")]
    [Arguments("entry")]
    [Arguments("source")]
    public async Task Unreadable_diagnostics_preserve_step_failure_and_retry_behavior(string failure)
    {
        var options = new BDTestOptions();
        options.RetryTestRules.Add(exception => exception is UnreadableDataException or UnreadableSourceException, 1);
        using var run = BDTestRun.Start(options);
        var host = BDTestScenario.For<object>(new object(), run);
        host.BeginTest("unreadable-diagnostics-" + failure);
        Exception exception = failure == "source" ? new UnreadableSourceException() : new UnreadableDataException(failure);
        var executions = 0;
        try
        {
            var thrown = await Assert.That(async () =>
                await host.When(() => Fail()).Then(() => { }).BDTestAsync()).Throws<Exception>();

            await Assert.That(thrown).IsSameReferenceAs(exception);
            await Assert.That(executions).IsEqualTo(2);
            var scenario = run.Scenarios.Single();
            await Assert.That(scenario.Status).IsEqualTo(Status.Failed);
            await Assert.That(scenario.RetryCount).IsEqualTo(1);

            await Assert.That((Required(scenario.Attempts).Select(attempt => Required(attempt.Exception).TypeName)).All(item => Equals(item, exception.GetType().FullName))).IsTrue();
            await Assert.That(Required(Required(scenario.Steps).Single(step => step.Status == Status.Failed).Exception).Message).IsEqualTo("original failure");
        }
        finally { host.EndTest(); }

        void Fail() { executions++; throw exception; }
    }

    [Test]
    public async Task Html_uses_structured_nodes_and_encodes_every_diagnostic_field()
    {
        var wrapper = ReportJson.DeserializeObject<ExceptionWrapper>("""
            {"TypeName":"<Outer>","Message":"<message>","StackTrace":"<stack>","Source":"<source>",
             "Data":{"<key>":"<value>"},"InnerExceptions":[
               {"TypeName":"<First>","Message":"<first>"},
               {"TypeName":"<Second>","Message":"<second>"}]}
            """);

        var html = ExceptionHtmlHelper.GetHtml(wrapper);

        await Assert.That(html).StartsWith("<details class=\"exception-tree\" open>");
        foreach (var field in new[] { "Outer", "message", "stack", "source", "key", "value", "First", "first", "Second", "second" })

            await Assert.That(html).Contains("&lt;" + field + "&gt;").And.DoesNotContain("<" + field + ">");
        await Assert.That(html.Split("<details class=\"exception-tree\">").Length - 1).IsEqualTo(2);
    }

    [Test]
    public async Task Deep_chains_stop_at_ten_nodes_and_keep_the_boundary_diagnostics()
    {
        Exception exception = new Exception("deepest");
        for (var index = 0; index < 1000; index++) exception = new Exception("level " + index, exception);

        var wrapper = new ExceptionWrapper(exception);
        var nodes = 1;
        var last = wrapper;
        while (Required(last.InnerExceptions).Count > 0) { last = last.InnerExceptions.Single(); nodes++; }

        await Assert.That(nodes).IsEqualTo(10);
        await Assert.That(last.TruncationReason).IsEqualTo("DepthLimit");
        await Assert.That(last.Message).IsEqualTo("level 990");
        await Assert.That(() => ReportJson.SerializeObject(wrapper)).ThrowsNothing();
    }

    [Test]
    public async Task Legacy_exceptions_derive_short_names_and_have_no_children()
    {
        var report = Required(BDTestJsonHelper.Deserialize("""
            {"Scenarios":[{"ExecutionException":{"TypeName":"Legacy.Errors.Failure","Message":"legacy","AsString":"original text"}}]}
            """));
        var wrapper = Required(Required(report.Scenarios).Single().Exception);

        await Assert.That(wrapper.ShortTypeName).IsEqualTo("Failure");
        await Assert.That(wrapper.InnerExceptions).IsEmpty();
        await Assert.That(wrapper.GetRootCauses().Single()).IsSameReferenceAs(wrapper);
        await Assert.That(wrapper.AsString).IsEqualTo("original text");
    }

    [Test]
    [Arguments("Legacy.Errors.Failure", "Failure")]
    [Arguments("Failure", "Failure")]
    [Arguments("Legacy.Container+Failure`1[[System.String, System.Private.CoreLib]]", "Failure`1")]
    public async Task Missing_short_names_exclude_namespaces_containers_and_generic_arguments(string typeName, string expected)
    {
        var wrapper = new ExceptionWrapper { TypeName = typeName };
        await Assert.That(wrapper.ShortTypeName).IsEqualTo(expected);
    }

    [Test]
    public async Task Grouping_uses_every_root_type_and_does_not_merge_same_named_types()
    {
        var first = new ExceptionWrapper(new TargetInvocationException(new First.Failure("same")));
        var second = new ExceptionWrapper(new AggregateException(new Second.Failure("same"), new First.Failure("different")));
        var groups = FailureGroup.Build([FailedScenario(first), FailedScenario(second)], FailureGroup.ByExceptionType);

        await Assert.That(groups).Count().IsEqualTo(2);
        await Assert.That(groups.Single(group => group.Key == typeof(First.Failure).FullName).Scenarios.Count).IsEqualTo(2);
        await Assert.That(groups.Single(group => group.Key == typeof(Second.Failure).FullName).Scenarios.Count).IsEqualTo(1);
    }

    private static Scenario FailedScenario(ExceptionWrapper exception)
    {
        var scenario = ReportJson.DeserializeObject<Scenario>("""{"Status":"Failed"}""");
        scenario.ExecutionException = exception;
        return scenario;
    }

    private static class First { public class Failure(string message) : Exception(message); }
    private static class Second { public class Failure(string message) : Exception(message); }
    private sealed class BadValue { public override string ToString() => throw new InvalidOperationException("formatter failure"); }

    private sealed class UnreadableSourceException() : Exception("original failure", new Exception("original cause"))
    {
        public override string? Source { get => throw new InvalidOperationException("source getter failed"); set { } }
    }

    private sealed class UnreadableDataException(string failure) : Exception("original failure", new Exception("original cause"))
    {
        public override IDictionary Data => failure switch
        {
            "getter" => throw new InvalidOperationException("data getter failed"),
            // Intentionally violate the BCL contract to test defensive exception capture.
            "null" => null!,
            _ => new UnreadableDictionary(failure)
        };
    }

    private sealed class UnreadableDictionary(string failure) : Hashtable
    {
        public override IDictionaryEnumerator GetEnumerator() => failure == "enumerator"
            ? throw new InvalidOperationException("enumerator failed")
            : new UnreadableEnumerator(failure);
    }

    private sealed class UnreadableEnumerator(string failure) : IDictionaryEnumerator
    {
        public bool MoveNext() => failure == "move" ? throw new InvalidOperationException("move failed") : true;
        public DictionaryEntry Entry => throw new InvalidOperationException("entry failed");
        public object Current => Entry;
        public object Key => Entry.Key;
        public object? Value => Entry.Value;
        public void Reset() => throw new NotSupportedException();
    }
}
