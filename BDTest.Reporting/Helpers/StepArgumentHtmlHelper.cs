using BDTest.Rendering;

namespace BDTest.Helpers;

/// <summary>Shared, HTML-encoded structured argument markup for hosted and standalone reports.</summary>
public static class StepArgumentHtmlHelper
{
    public static string GetHtml(IReadOnlyList<global::BDTest.StepArgument>? arguments) =>
        GetHtml(arguments?.Select(argument => argument.ToResult()).ToArray());

    public static string GetHtml(IReadOnlyList<Contracts.StepArgument>? arguments) =>
        ReportHtmlRenderer.RenderArguments(arguments);
}
