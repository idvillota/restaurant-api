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

    /// <summary>Administrator Gral. (and legacy Owner). Not Manager (“Administrator”).</summary>
    public static bool IsGeneralAdministrator(IEnumerable<string>? roleNames) =>
        roleNames is not null &&
        roleNames.Any(name =>
            string.Equals(name, Administrator, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, Owner, StringComparison.OrdinalIgnoreCase));
}
