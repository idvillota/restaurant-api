namespace Restaurant.Infrastructure.Authorization;

public static class SystemRoles
{
    public const string Administrator = "Administrator";
    public const string Manager = "Manager";
    public const string Waitress = "Waitress";
    public const string Cashier = "Cashier";

    /// <summary>Legacy name kept for migrating existing databases.</summary>
    public const string Owner = "Owner";

    /// <summary>Legacy name kept for migrating existing databases.</summary>
    public const string Staff = "Staff";

    public static IReadOnlyList<string> All { get; } =
        [Administrator, Manager, Waitress, Cashier];
}
