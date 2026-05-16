using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Models;

namespace Restaurant.Infrastructure.Common;

internal static class ListQueryHelpers
{
    public static async Task<PagedResult<TDto>> ToPagedResultAsync<TEntity, TDto>(
        IQueryable<TEntity> query,
        ListQuery listQuery,
        Func<IQueryable<TEntity>, IQueryable<TEntity>> shapeQuery,
        Func<IEnumerable<TEntity>, Task<IReadOnlyList<TDto>>> mapPageAsync,
        CancellationToken cancellationToken)
    {
        var (page, pageSize) = listQuery.Normalize();
        query = shapeQuery(query);

        var totalCount = await query.CountAsync(cancellationToken);
        var entities = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = await mapPageAsync(entities);

        return new PagedResult<TDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
        };
    }

    public static string SearchTerm(ListQuery query) => query.Search?.Trim().ToLowerInvariant() ?? string.Empty;

    public static bool TryParseBool(string? value, out bool result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = false;
            return false;
        }

        if (bool.TryParse(value, out result))
            return true;

        if (value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("yes", StringComparison.OrdinalIgnoreCase))
        {
            result = true;
            return true;
        }

        if (value.Equals("0", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("no", StringComparison.OrdinalIgnoreCase))
        {
            result = false;
            return true;
        }

        return false;
    }

    public static bool TryParseGuid(string? value, out Guid result) =>
        Guid.TryParse(value, out result);

    public static bool TryParseDecimal(string? value, out decimal result) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result);

    public static bool TryParseInt(string? value, out int result) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
}
