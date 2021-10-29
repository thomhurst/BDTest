namespace BDTest.NetCore.Razor.ReportMiddleware.Constants
{
    public class PagerQueryParameters
    {
        public const string Page = "page";
        public const string All = "all";
    }

    public class PaginationInformation
    {
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public int StartPage { get; set; }
        public int EndPage { get; set; }
    }
}