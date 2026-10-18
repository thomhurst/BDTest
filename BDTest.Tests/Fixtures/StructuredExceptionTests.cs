using System.Reflection;
using BDTest.Helpers;
using BDTest.Maps;
using BDTest.Razor.Reports.Models;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class StructuredExceptionTests
{
    [Test]
    public void Nested_and_aggregate_exceptions_round_trip_with_diagnostics()
    {
        var cause = new InvalidOperationException("<actual>") { Source = "<source>" };
        cause.Data["<expected>"] = 42;
        cause.Data["null"] = null;
        var exception = new TargetInvocationException(new AggregateException("aggregate", cause, new ArgumentException("other")));
        var wrapper = new ExceptionWrapper(exception);
        var report = new BDTestOutputModel { Scenarios = [new Scenario { ExecutionException = wrapper }] };

        var json = BDTestJsonHelper.Serialize(report);
        var restored = BDTestJsonHelper.Deserialize(json).Scenarios.Single().Exception;

        Assert.That(restored.TypeName, Is.EqualTo(typeof(TargetInvocationException).FullName));
        Assert.That(restored.ShortTypeName, Is.EqualTo(nameof(TargetInvocationException)));
        Assert.That(restored.IsWrapper, Is.True);
        Assert.That(restored.AsString, Is.EqualTo(exception.ToString()));
        var children = restored.InnerExceptions.Single().InnerExceptions;
        Assert.That(children, Has.Count.EqualTo(2));
        Assert.That(children[0].Data["<expected>"], Is.EqualTo("42"));
        Assert.That(children[0].Data["null"], Is.EqualTo(""));
        Assert.That(children[0].Source, Is.EqualTo("<source>"));
        Assert.That(children[0].HResult, Is.EqualTo(cause.HResult));
        Assert.That(restored.GetRootCauses().Select(item => item.TypeName),
            Is.EqualTo(new[] { typeof(InvalidOperationException).FullName, typeof(ArgumentException).FullName }));
        Assert.That(BDTestJsonHelper.Serialize(BDTestJsonHelper.Deserialize(json)), Is.EqualTo(json));
    }

    [Test]
    public void Cycles_are_recorded_without_calling_recursive_ToString()
    {
        var exception = new Exception("cycle");
        typeof(Exception).GetField("_innerException", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(exception, exception);
        exception.Data["self"] = exception;

        var wrapper = new ExceptionWrapper(exception);

        Assert.That(wrapper.InnerExceptions.Single().TruncationReason, Is.EqualTo("Cycle"));
        Assert.That(wrapper.AsString, Does.Contain("cycle"));
        Assert.That(wrapper.Data["self"], Is.EqualTo(typeof(Exception).FullName));
        Assert.That(wrapper.GetRootCauses(), Has.Count.EqualTo(1));
        Assert.DoesNotThrow(() => ReportJson.SerializeObject(wrapper));
    }

    [Test]
    public void Deep_aggregate_messages_do_not_traverse_beyond_the_depth_limit()
    {
        Exception exception = new Exception("deepest");
        for (var index = 0; index < 1000; index++) exception = new AggregateException("aggregate " + index, exception);

        var wrapper = new ExceptionWrapper(exception);

        Assert.That(wrapper.Message, Does.Contain("truncated"));
        Assert.That(wrapper.GetRootCauses().Single().TruncationReason, Is.EqualTo("DepthLimit"));
        Assert.DoesNotThrow(() => ReportJson.SerializeObject(wrapper));
    }

    [Test]
    public void Repeated_aggregate_children_are_preserved_and_not_treated_as_cycles()
    {
        var cause = new InvalidOperationException("repeated");
        var wrapper = new ExceptionWrapper(new AggregateException(cause, cause));

        Assert.That(wrapper.InnerExceptions, Has.Count.EqualTo(2));
        Assert.That(wrapper.InnerExceptions.All(child => child.TruncationReason == null), Is.True);
        Assert.That(wrapper.GetRootCauses(), Has.Count.EqualTo(2));
        var groups = FailureGroup.Build([FailedScenario(wrapper)], FailureGroup.ByExceptionType);
        Assert.That(groups.Single().Scenarios, Has.Count.EqualTo(1));
    }

    [Test]
    public void Data_preserves_string_key_collisions_and_survives_throwing_formatters()
    {
        var exception = new Exception("data");
        exception.Data[1] = "first";
        exception.Data["1"] = "second";
        exception.Data["bad"] = new BadValue();

        var wrapper = new ExceptionWrapper(exception);

        Assert.That(wrapper.Data.Values, Is.EquivalentTo(new[] { "first", "second", "[Value could not be formatted]" }));
    }

    [Test]
    public void Html_uses_structured_nodes_and_encodes_every_diagnostic_field()
    {
        var wrapper = ReportJson.DeserializeObject<ExceptionWrapper>("""
            {"TypeName":"<Outer>","Message":"<message>","StackTrace":"<stack>","Source":"<source>",
             "Data":{"<key>":"<value>"},"InnerExceptions":[
               {"TypeName":"<First>","Message":"<first>"},
               {"TypeName":"<Second>","Message":"<second>"}]}
            """);

        var html = ExceptionHtmlHelper.GetHtml(wrapper);

        Assert.That(html, Does.StartWith("<details class=\"exception-tree\" open>"));
        foreach (var field in new[] { "Outer", "message", "stack", "source", "key", "value", "First", "first", "Second", "second" })
            Assert.That(html, Does.Contain("&lt;" + field + "&gt;").And.Not.Contain("<" + field + ">"));
        Assert.That(html.Split("<details class=\"exception-tree\">").Length - 1, Is.EqualTo(2));
    }

    [Test]
    public void Deep_chains_stop_at_ten_nodes_and_keep_the_boundary_diagnostics()
    {
        Exception exception = new Exception("deepest");
        for (var index = 0; index < 1000; index++) exception = new Exception("level " + index, exception);

        var wrapper = new ExceptionWrapper(exception);
        var nodes = 1;
        var last = wrapper;
        while (last.InnerExceptions.Count > 0) { last = last.InnerExceptions.Single(); nodes++; }

        Assert.That(nodes, Is.EqualTo(10));
        Assert.That(last.TruncationReason, Is.EqualTo("DepthLimit"));
        Assert.That(last.Message, Is.EqualTo("level 990"));
        Assert.DoesNotThrow(() => ReportJson.SerializeObject(wrapper));
    }

    [Test]
    public void Legacy_exceptions_derive_short_names_and_have_no_children()
    {
        var report = BDTestJsonHelper.Deserialize("""
            {"Scenarios":[{"ExecutionException":{"TypeName":"Legacy.Errors.Failure","Message":"legacy","AsString":"original text"}}]}
            """);
        var wrapper = report.Scenarios.Single().Exception;

        Assert.That(wrapper.ShortTypeName, Is.EqualTo("Failure"));
        Assert.That(wrapper.InnerExceptions, Is.Empty);
        Assert.That(wrapper.GetRootCauses().Single(), Is.SameAs(wrapper));
        Assert.That(wrapper.AsString, Is.EqualTo("original text"));
    }

    [TestCase("Legacy.Errors.Failure", "Failure")]
    [TestCase("Failure", "Failure")]
    [TestCase("Legacy.Container+Failure`1[[System.String, System.Private.CoreLib]]", "Failure`1")]
    public void Missing_short_names_exclude_namespaces_containers_and_generic_arguments(string typeName, string expected)
    {
        var wrapper = new ExceptionWrapper { TypeName = typeName };
        Assert.That(wrapper.ShortTypeName, Is.EqualTo(expected));
    }

    [Test]
    public void Grouping_uses_every_root_type_and_does_not_merge_same_named_types()
    {
        var first = new ExceptionWrapper(new TargetInvocationException(new First.Failure("same")));
        var second = new ExceptionWrapper(new AggregateException(new Second.Failure("same"), new First.Failure("different")));
        var groups = FailureGroup.Build([FailedScenario(first), FailedScenario(second)], FailureGroup.ByExceptionType);

        Assert.That(groups, Has.Count.EqualTo(2));
        Assert.That(groups.Single(group => group.Key == typeof(First.Failure).FullName).Scenarios.Count, Is.EqualTo(2));
        Assert.That(groups.Single(group => group.Key == typeof(Second.Failure).FullName).Scenarios.Count, Is.EqualTo(1));
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
}
