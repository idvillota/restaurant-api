using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Sales.LunchTickets;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Persistence;
using Restaurant.Infrastructure.SalesReceipts;

namespace Restaurant.Infrastructure.Services;

public sealed class TiqueteraSalesService(
    ApplicationDbContext db,
    ICurrentTenantContext tenantContext,
    ICashierShiftService cashierShifts,
    ILunchTicketBookletService lunchTickets,
    IUnitOfWork unitOfWork) : ITiqueteraSalesService
{
    public const string DefaultCustomerTaxId = BillService.DefaultCustomerTaxId;
    public const string DefaultCustomerName = BillService.DefaultCustomerName;

    public async Task<SellTiqueteraPackResultDto> SellPackAsync(
        SellTiqueteraPackDto dto,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        if (!settings.TiqueterasEnabled)
            throw new InvalidOperationException("Este local no tiene tiqueteras habilitadas.");

        if (dto.PaymentMethod is PaymentMethod.Tiquetera)
            throw new InvalidOperationException(
                "La venta del paquete se paga con efectivo, tarjeta u otro método — no con Tiquetera.");

        if (dto.Quantity < 1)
            throw new InvalidOperationException("La cantidad debe ser al menos 1.");

        var pack = await db.Products
            .Include(p => p.ProductType)
            .FirstOrDefaultAsync(
                p => p.Id == dto.PackProductId && p.IsActive && p.LunchTicketPackSize > 0,
                cancellationToken)
            ?? throw new InvalidOperationException("Paquete de tiquetera no encontrado o inactivo.");

        var customer = await db.Customers.FirstOrDefaultAsync(
            c => c.Id == dto.CustomerId && c.IsActive,
            cancellationToken)
            ?? throw new InvalidOperationException("Cliente no encontrado o inactivo.");

        if (string.Equals(customer.TaxId, DefaultCustomerTaxId, StringComparison.Ordinal)
            || string.Equals(customer.Name, DefaultCustomerName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Selecciona el cliente dueño de la tiquetera (no uses Consumidor final).");
        }

        var (shiftId, processedByUserId) = await cashierShifts.RequireOpenShiftAsync(cancellationToken);
        var cashierName = await ResolveCashierDisplayNameAsync(processedByUserId, cancellationToken);

        var quantity = dto.Quantity;
        var unitPrice = pack.UnitPrice;
        var subtotal = decimal.Round(unitPrice * quantity, 2, MidpointRounding.AwayFromZero);
        var (impoconsumo, totalDue) = BillCheckoutCalculator.ComputeTotals(
            subtotal,
            discountAmount: 0m,
            tipAmount: 0m,
            settings.ImpoconsumoPercent);
        var impoconsumoBase =
            settings.ImpoconsumoPercent > 0 && subtotal > 0
                ? decimal.Round(subtotal / (1m + (settings.ImpoconsumoPercent / 100m)), 2, MidpointRounding.AwayFromZero)
                : subtotal;

        var now = DateTime.UtcNow;
        var billId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var billNumber = await GenerateBillNumberAsync(cancellationToken);
        var dianConsecutive = await AllocateDianConsecutiveAsync(settings, cancellationToken);
        var invoiceNumber = BillCheckoutCalculator.FormatInvoiceDisplayNumber(
            settings.InvoiceNumberPrefix,
            dianConsecutive);

        var bill = new Bill
        {
            Id = billId,
            CustomerId = customer.Id,
            Number = billNumber,
            Status = BillStatus.Paid,
            Subtotal = subtotal,
            DiscountAmount = 0m,
            TipAmount = 0m,
            TaxAmount = impoconsumo,
            Total = totalDue,
            IssuedAtUtc = now,
            PaidAtUtc = now,
            CashierShiftId = shiftId,
            ProcessedByUserId = processedByUserId,
            DianConsecutiveNumber = dianConsecutive,
            ProcessedByDisplayName = cashierName,
            OrderNumbersSnapshot = "Tiquetera",
        };
        await db.Bills.AddAsync(bill, cancellationToken);

        await db.BillLines.AddAsync(
            new BillLine
            {
                Id = Guid.NewGuid(),
                BillId = billId,
                ProductId = pack.Id,
                ProductName = pack.Name,
                ProductTypeName = pack.ProductType?.Name ?? "Tiqueteras",
                Quantity = quantity,
                UnitPrice = unitPrice,
                LineTotal = subtotal,
                ImpoconsumoAmount = BillCheckoutCalculator.AllocateLineImpoconsumo(
                    subtotal,
                    subtotal,
                    impoconsumo),
            },
            cancellationToken);

        await db.Invoices.AddAsync(
            new Invoice
            {
                Id = invoiceId,
                BillId = billId,
                CustomerId = customer.Id,
                Number = invoiceNumber,
                Status = InvoiceStatus.Paid,
                Subtotal = impoconsumoBase,
                TaxAmount = impoconsumo,
                Total = totalDue,
                IssuedAtUtc = now,
            },
            cancellationToken);

        await db.Payments.AddAsync(
            new Payment
            {
                Id = Guid.NewGuid(),
                BillId = billId,
                InvoiceId = invoiceId,
                Amount = totalDue,
                Method = dto.PaymentMethod,
                Status = PaymentStatus.Completed,
                ExternalReference = string.IsNullOrWhiteSpace(dto.ExternalReference)
                    ? null
                    : dto.ExternalReference.Trim(),
                PaidAtUtc = now,
                CashierShiftId = shiftId,
                ProcessedByUserId = processedByUserId,
            },
            cancellationToken);

        await lunchTickets.CreateFromPackSaleAsync(
            customer.Id,
            billId,
            pack.Id,
            pack.LunchTicketPackSize!.Value,
            quantity,
            unitPrice,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var remaining = await lunchTickets.GetRemainingTicketsAsync(customer.Id, cancellationToken);
        var ticketsGranted = pack.LunchTicketPackSize.Value * quantity;

        return new SellTiqueteraPackResultDto
        {
            BillId = billId,
            BillNumber = billNumber,
            CustomerId = customer.Id,
            CustomerName = customer.Name,
            PackProductName = pack.Name,
            TicketsGranted = ticketsGranted,
            RemainingTickets = remaining,
            TotalPaid = totalDue,
            PaymentMethod = dto.PaymentMethod,
        };
    }

    private async Task<TenantSettings> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var tenantId = ResolveTenantId();
        return await db.TenantSettings.FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Configuración del local no encontrada.");
    }

    private async Task<int> AllocateDianConsecutiveAsync(
        TenantSettings settings,
        CancellationToken cancellationToken)
    {
        if (settings.DianResolutionFrom <= 0 || settings.DianResolutionTo < settings.DianResolutionFrom)
            throw new InvalidOperationException(
                "Configure el rango de numeración DIAN en Configuración del local (desde / hasta).");

        var maxUsed = await db.Bills
            .Where(b => b.DianConsecutiveNumber > 0)
            .MaxAsync(b => (int?)b.DianConsecutiveNumber, cancellationToken) ?? 0;

        var next = settings.DianNextConsecutive;
        if (next < settings.DianResolutionFrom)
            next = settings.DianResolutionFrom;
        if (next <= maxUsed)
            next = maxUsed + 1;

        if (next > settings.DianResolutionTo)
            throw new InvalidOperationException(
                "Se agotó el rango de consecutivos DIAN. Actualice la resolución en Configuración.");

        settings.DianNextConsecutive = next + 1;
        db.TenantSettings.Update(settings);
        return next;
    }

    private async Task<string> GenerateBillNumberAsync(CancellationToken cancellationToken)
    {
        var tenantId = ResolveTenantId();
        var dayPrefix = $"BILL-{ColombiaTime.NowStamp("yyyyMMdd")}-";
        var latestNumber = await db.Bills
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.Number.StartsWith(dayPrefix))
            .OrderByDescending(b => b.Number)
            .Select(b => b.Number)
            .FirstOrDefaultAsync(cancellationToken);

        var next = 1;
        if (latestNumber is not null
            && latestNumber.Length > dayPrefix.Length
            && int.TryParse(latestNumber[dayPrefix.Length..], out var parsed))
        {
            next = parsed + 1;
        }

        return $"{dayPrefix}{next:D4}";
    }

    private async Task<string?> ResolveCashierDisplayNameAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        return user?.Email;
    }

    private Guid ResolveTenantId()
    {
        if (tenantContext.TenantId is { } tenantId && tenantId != Guid.Empty)
            return tenantId;
        throw new InvalidOperationException("Tenant context is not available.");
    }
}
