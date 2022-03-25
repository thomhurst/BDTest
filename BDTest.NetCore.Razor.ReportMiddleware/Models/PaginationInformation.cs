namespace BDTest.NetCore.Razor.ReportMiddleware.Models;

public class PaginationInformation
{
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public int StartPage { get; set; }
    public int EndPage { get; set; }
}