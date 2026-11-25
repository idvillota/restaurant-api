using Microsoft.EntityFrameworkCore;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Infrastructure.Common;

internal static class StockControlHelper
{
    /// <summary>
    /// Resolves effective stock mode. ProductUnits is authoritative when set;
    /// otherwise falls back to InventoryEnabled for legacy rows/tests.
    /// </summary>
    internal static StockControlMode Resolve(StockControlMode mode, bool inventoryEnabled) =>
        mode == StockControlMode.ProductUnits
            ? StockControlMode.ProductUnits
            : inventoryEnabled
                ? StockControlMode.Ingredients
                : StockControlMode.None;

    internal static async Task<StockControlMode> GetModeAsync(
        ApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var row = await db.TenantSettings.AsNoTracking()
            .Select(s => new { s.StockControlMode, s.InventoryEnabled })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
            return StockControlMode.Ingredients;

        return Resolve(row.StockControlMode, row.InventoryEnabled);
    }

    internal static void ApplyToSettings(
        Domain.Entities.TenantSettings settings,
        int? stockControlMode,
        bool inventoryEnabled)
    {
        if (stockControlMode is int raw && Enum.IsDefined(typeof(StockControlMode), raw))
        {
            settings.StockControlMode = (StockControlMode)raw;
            settings.InventoryEnabled = settings.StockControlMode == StockControlMode.Ingredients;
            return;
        }

        // Legacy clients that only send inventoryEnabled.
        settings.InventoryEnabled = inventoryEnabled;
        if (settings.StockControlMode != StockControlMode.ProductUnits || inventoryEnabled)
            settings.StockControlMode = inventoryEnabled ? StockControlMode.Ingredients : StockControlMode.None;
    }
}
