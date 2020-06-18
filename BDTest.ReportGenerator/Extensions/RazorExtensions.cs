using System.Reflection;
using BDTest.ReportGenerator.Builders.Razor;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.FileProviders;

namespace BDTest.ReportGenerator.Extensions
{
    public static class RazorExtensions
    {
        public static RazorViewEngineOptions AddBDTestViews(this RazorViewEngineOptions options)
        {
            options.FileProviders.Add(new EmbeddedFileProvider(
                typeof(RazorViewToStringRenderer).GetTypeInfo().Assembly,
                "BDTest.ReportGenerator"
            ));

            return options;
        }
    }
}