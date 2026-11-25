using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Sales.LunchTickets;
using Restaurant.Domain.Entities;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Infrastructure.Services;

public sealed class LunchTicketBookletService(
    ApplicationDbContext db,
    ICurrentTenantContext tenantContext) : ILunchTicketBookletService
{
    public async Task<LunchTicketBalanceDto> GetBalanceAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var customer = await db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken)
            ?? throw new InvalidOperationException("Cliente no encontrado.");

        var booklets = await db.LunchTicketBooklets.AsNoTracking()
            .Include(b => b.PackProduct)
            .Where(b => b.CustomerId == customerId)
            .OrderByDescending(b => b.PurchasedAtUtc)
            .ToListAsync(cancellationToken);

        var purchased = booklets.Sum(b => b.TotalTickets);
        var remaining = booklets.Sum(b => b.RemainingTickets);

        return new LunchTicketBalanceDto
        {
            CustomerId = customer.Id,
            CustomerName = customer.Name,
            RemainingTickets = remaining,
            TotalTicketsPurchased = purchased,
            UsedTickets = Math.Max(0, purchased - remaining),
            Booklets = booklets.Select(b => new LunchTicketBookletSummaryDto
            {
                Id = b.Id,
                PackProductName = b.PackProduct.Name,
                TotalTickets = b.TotalTickets,
                RemainingTickets = b.RemainingTickets,
                UsedTickets = Math.Max(0, b.TotalTickets - b.RemainingTickets),
                PurchasedAtUtc = b.PurchasedAtUtc,
            }).ToList(),
        };
    }

    public async Task<int> GetRemainingTicketsAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        await db.LunchTicketBooklets.AsNoTracking()
            .Where(b => b.CustomerId == customerId)
            .SumAsync(b => (int?)b.RemainingTickets, cancellationToken) ?? 0;

    public async Task CreateFromPackSaleAsync(
        Guid customerId,
        Guid billId,
        Guid packProductId,
        int ticketsPerPack,
        decimal quantity,
        decimal packUnitPrice,
        CancellationToken cancellationToken = default)
    {
        if (ticketsPerPack <= 0 || quantity <= 0)
            return;

        var tickets = (int)Math.Round(ticketsPerPack * quantity, MidpointRounding.AwayFromZero);
        if (tickets <= 0)
            return;

        await db.LunchTicketBooklets.AddAsync(
            new LunchTicketBooklet
            {
                Id = Guid.NewGuid(),
                TenantId = ResolveTenantId(),
                CustomerId = customerId,
                PurchaseBillId = billId,
                PackProductId = packProductId,
                TotalTickets = tickets,
                RemainingTickets = tickets,
                PackUnitPrice = packUnitPrice,
                PurchasedAtUtc = DateTime.UtcNow,
            },
            cancellationToken);
    }

    public async Task RedeemAsync(Guid customerId, int tickets, CancellationToken cancellationToken = default)
    {
        if (tickets <= 0)
            throw new InvalidOperationException("La cantidad a redimir debe ser mayor que cero.");

        var booklets = await db.LunchTicketBooklets
            .Where(b => b.CustomerId == customerId && b.RemainingTickets > 0)
            .OrderBy(b => b.PurchasedAtUtc)
            .ToListAsync(cancellationToken);

        var available = booklets.Sum(b => b.RemainingTickets);
        if (available < tickets)
            throw new InvalidOperationException(
                $"La tiquetera no tiene cupos suficientes (disponibles: {available}, requeridos: {tickets}).");

        var remaining = tickets;
        foreach (var booklet in booklets)
        {
            if (remaining <= 0)
                break;

            var take = Math.Min(booklet.RemainingTickets, remaining);
            booklet.RemainingTickets -= take;
            remaining -= take;
        }
    }

    private Guid ResolveTenantId()
    {
        if (tenantContext.TenantId is { } tenantId && tenantId != Guid.Empty)
            return tenantId;
        throw new InvalidOperationException("Tenant context is not available.");
    }
}
