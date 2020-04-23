namespace BDTest.ReportGenerator.Helpers
{
    internal static class VersionHelper
    {
        internal static readonly string CurrentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(); 
    }
}