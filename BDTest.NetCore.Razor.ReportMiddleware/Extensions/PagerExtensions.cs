using System;
using System.Linq;
using BDTest.NetCore.Razor.ReportMiddleware.Models;

namespace BDTest.NetCore.Razor.ReportMiddleware.Extensions
{
    public static class PagerExtensions
    {
        public static int? GetPageNumberWhere<T>(this Pager<T> pager, Func<T, bool> predicate)
        {
            var item = pager.AllItems.FirstOrDefault(predicate);
            
            return item == null ? null : GetPageNumberOfItem(pager, item);
        }
        
        public static int? GetPageNumberOfItem<T>(this Pager<T> pager, T item)
        {
            var items = pager.AllItems;
            
            var indexOfItem = Array.IndexOf(items, item);

            for (var i = 1; i <= pager.TotalPages; i++)
            {
                var itemsIteratedThrough = i * pager.PageSize;
                if (indexOfItem <= itemsIteratedThrough)
                {
                    return i;
                }
            }

            return null;
        }
    }
}