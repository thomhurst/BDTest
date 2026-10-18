using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BDTest.Razor.Reports.Security;

/// <summary>Adds the response nonce to scripts authored in the report Razor views.</summary>
[HtmlTargetElement("script")]
public class ReportScriptNonceTagHelper : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext? ViewContext { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.Attributes.SetAttribute("nonce", ReportSecurityHeaders.GetNonce((ViewContext ?? throw new InvalidOperationException("MVC must supply a view context.")).HttpContext));
    }
}
