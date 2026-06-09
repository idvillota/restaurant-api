namespace Restaurant.Infrastructure.Common;

internal static class EfTextSearch
{
    public static string LikePattern(string term) => $"%{term}%";
}
