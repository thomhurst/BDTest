using System;
using System.IO;
using BDTest.Paths;

namespace BDTest.ReportGenerator
{
    public static class JavascriptStringHelpers
    {
        public static string LoadJavascriptChartsAsync(string chartJavascriptCode) => @"
LoadChartScriptAsync();

function LoadChartScriptAsync() {
    let scriptPath = ""https://www.gstatic.com/charts/loader.js""
    let script = document.createElement('script');

    script.onerror = function () {
        console.log(""Error loading script: "" + scriptPath)
    }

    script.onload = function () {
        console.log(scriptPath + ' loaded ')
"
                                                                                      +
                                                                                      BuildDynamicCreateElementInJavascript(chartJavascriptCode)
                                                                                      +
                                                                                      @"
        RefreshChartElements();
    }

    script.src = scriptPath;
    document.getElementsByTagName('head')[0].appendChild(script);
}

function RefreshChartElements() {
    let elements = document.getElementsByName(""piechart"")

    Array.from(elements).forEach((element) => {
        var content = element.innerHTML;
        element.innerHTML = content;
    })
}
";

        private static string BuildDynamicCreateElementInJavascript(string javascriptBody)
        {
            // Write body to its own javascript file
            var uniqueFilename = $"{Guid.NewGuid():N}.js";
            var javascriptFilePath = Path.Combine(FileLocations.OutputDirectory, uniqueFilename);
            File.WriteAllText(javascriptFilePath, javascriptBody);
            
            return @"
let script = document.createElement('script');

    script.onerror = function () {
        console.log(""Error loading chart script"")
    }

    script.src = '" + javascriptFilePath.Replace("\\", "/") + @"';
    document.getElementsByTagName('head')[0].appendChild(script);
";
        }
    }
}