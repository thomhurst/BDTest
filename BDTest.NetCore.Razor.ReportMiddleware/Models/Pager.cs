using System;
using System.Collections.Generic;
using System.Linq;
using BDTest.NetCore.Razor.ReportMiddleware.Constants;

namespace BDTest.NetCore.Razor.ReportMiddleware.Models
{
    public class Pager<T>
    {
        public T[] AllItems { get; }

        public Pager(T[] allItems, string currentPageString) : 
            this(allItems, currentPageString, 25, int.MaxValue)
        {
        }
        
        public Pager(T[] allItems, string currentPageString, int pageSize) : 
            this(allItems, currentPageString, pageSize, int.MaxValue)
        {
        }
        
        public Pager(
            T[] allItems,
            string currentPageString,
            int pageSize,
            int maxPages)
        {
            AllItems = allItems;
            var totalItems = allItems.Length;

            if (totalItems == 0)
            {
                return;
            }

            if (!int.TryParse(currentPageString, out var currentPage))
            {
                currentPage = 1;
                pageSize = int.MaxValue;
            }
            
            // calculate total pages
            var totalPages = (int) Math.Ceiling((decimal) totalItems / (decimal) pageSize);

            // ensure current page isn't out of range
            if (currentPage < 1)
            {
                currentPage = 1;
            }
            else if (currentPage > totalPages)
            {
                currentPage = totalPages;
            }

            int startPage, endPage;
            if (totalPages <= maxPages)
            {
                // total pages less than max so show all pages
                startPage = 1;
                endPage = totalPages;
            }
            else
            {
                // total pages more than max so calculate start and end pages
                var maxPagesBeforeCurrentPage = (int) Math.Floor((decimal) maxPages / (decimal) 2);
                var maxPagesAfterCurrentPage = (int) Math.Ceiling((decimal) maxPages / (decimal) 2) - 1;
                if (currentPage <= maxPagesBeforeCurrentPage)
                {
                    // current page near the start
                    startPage = 1;
                    endPage = maxPages;
                }
                else if (currentPage + maxPagesAfterCurrentPage >= totalPages)
                {
                    // current page near the end
                    startPage = totalPages - maxPages + 1;
                    endPage = totalPages;
                }
                else
                {
                    // current page somewhere in the middle
                    startPage = currentPage - maxPagesBeforeCurrentPage;
                    endPage = currentPage + maxPagesAfterCurrentPage;
                }
            }

            // calculate start and end item indexes
            var startIndex = (currentPage - 1) * pageSize;
            var endIndex = Math.Min(startIndex + pageSize - 1, totalItems - 1);

            // create an array of pages that can be looped over
            var pages = Enumerable.Range(startPage, (endPage + 1) - startPage);

            // update object instance with all pager properties required by the view
            TotalItems = totalItems;
            CurrentPage = currentPage;
            PageSize = pageSize;
            TotalPages = totalPages;
            StartPage = startPage;
            EndPage = endPage;
            StartIndex = startIndex;
            EndIndex = endIndex;
            Pages = pages;
            ItemsForCurrentPage = allItems[startIndex..(endIndex+1)];
        }

        public int TotalItems { get; }
        public int CurrentPage { get; } = 1;
        public int PageSize { get; }
        public int TotalPages { get; } = 1;
        public int StartPage { get; } = 1;
        public int EndPage { get; } = 1;
        public int StartIndex { get; }
        public int EndIndex { get; }
        public IEnumerable<int> Pages { get; } = Enumerable.Empty<int>();
        public IEnumerable<T> ItemsForCurrentPage { get; } = Enumerable.Empty<T>();

        public PaginationInformation GetPaginationInformation() => new()
        {
            CurrentPage = CurrentPage,
            EndPage = EndPage,
            PageSize = PageSize,
            StartPage = StartPage,
            TotalPages = TotalPages
        };
    }
}