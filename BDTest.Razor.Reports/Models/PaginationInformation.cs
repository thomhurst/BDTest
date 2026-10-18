namespace BDTest.Razor.Reports.Models;

public class PaginationInformation
{
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public int StartPage { get; set; }
    public int EndPage { get; set; }

    /// <summary>Total number of items across every page.</summary>
    public int TotalItems { get; set; }

    /// <summary>Zero-based index of the first item on the current page.</summary>
    public int StartIndex { get; set; }

    /// <summary>Zero-based index of the last item on the current page.</summary>
    public int EndIndex { get; set; }

    /// <summary>Plural noun for the counted items, for example "runs".</summary>
    public string ItemName { get; set; } = "items";

    /// <summary>Label for the link to the previous page.</summary>
    public string PreviousText { get; set; } = "Previous";

    /// <summary>Label for the link to the next page.</summary>
    public string NextText { get; set; } = "Next";

    /// <summary>Page sizes offered in the footer. Empty hides the selector.</summary>
    public int[] PageSizeOptions { get; set; } = Array.Empty<int>();
}
