using System;
using System.Collections.Generic;
using System.Linq;

namespace FaceAttendanceSystem.Infrastructure
{
    public interface IPagedResult
    {
        int Page { get; }
        int PageSize { get; }
        int TotalCount { get; }
        int TotalPages { get; }
    }

    public class PagedResult<T> : IPagedResult
    {
        public PagedResult(IList<T> items, int page, int pageSize, int totalCount)
        {
            Items = items;
            Page = page;
            PageSize = pageSize;
            TotalCount = totalCount;
        }

        public IList<T> Items { get; }
        public int Page { get; }
        public int PageSize { get; }
        public int TotalCount { get; }
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    }

    public static class PagingExtensions
    {
        /// <summary>Pages an already-ordered query.</summary>
        public static PagedResult<T> ToPaged<T>(this IQueryable<T> orderedQuery, int page, int pageSize = 25)
        {
            var total = orderedQuery.Count();
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            page = Math.Min(Math.Max(1, page), totalPages);
            var items = orderedQuery.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return new PagedResult<T>(items, page, pageSize, total);
        }
    }
}
