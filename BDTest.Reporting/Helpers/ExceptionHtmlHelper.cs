using BDTest.Contracts;
using BDTest.Rendering;

namespace BDTest.Helpers;

/// <summary>Shared, HTML-encoded exception trees for hosted and standalone reports.</summary>
public static class ExceptionHtmlHelper
{
    public static string GetHtml(Test.ExceptionWrapper? exception) => GetHtml(exception?.ToResult());

    public static string GetHtml(ExceptionInfo? exception) => ReportHtmlRenderer.RenderException(exception);
}
