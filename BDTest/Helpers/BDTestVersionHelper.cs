namespace BDTest.Helpers
{
    public static class BDTestVersionHelper
    {
        public static readonly string CurrentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(); 
    }
}