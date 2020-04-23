namespace BDTest.ReportGenerator.Helpers
{
    internal static class VersionHelper
    {
        public static string CurrentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(); 
    }
}