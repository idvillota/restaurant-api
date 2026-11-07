using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Planillas;

namespace Restaurant.Tests.Unit.Infrastructure.Planillas;

public sealed class PlanillaDocumentFactoryTests
{
    [Fact]
    public void Build_splits_vitrina_and_includes_all_egresos_and_caja_purchases()
    {
        var model = PlanillaDocumentFactory.Build(
            "Nuestro Café",
            new DateOnly(2026, 11, 6),
            isDailyPlanilla: true,
            separateVitrina: true,
            dayNotes: "Cierre",
            [
                new PlanillaShiftInput
                {
                    CashierEmail = "cajero@test.local",
                    OpenedAtUtc = DateTime.UtcNow.AddHours(-8),
                    ClosedAtUtc = DateTime.UtcNow,
                    OpeningFloat = 50m,
                    ExpectedCash = 90m,
                    CountedCash = 80m,
                    Notes = "Faltante",
                    Payments =
                    [
                        new PlanillaPaymentInput
                        {
                            BillId = Guid.NewGuid(),
                            Amount = 100m,
                            Method = PaymentMethod.Cash,
                        },
                    ],
                    BillLines =
                    [
                        new PlanillaBillLineInput
                        {
                            BillId = Guid.NewGuid(),
                            ProductTypeName = "Bebidas",
                            Quantity = 1,
                            LineTotal = 70m,
                        },
                        new PlanillaBillLineInput
                        {
                            BillId = Guid.NewGuid(),
                            ProductTypeName = "Vitrina Café",
                            Quantity = 2,
                            LineTotal = 30m,
                        },
                    ],
                    Outflows =
                    [
                        new PlanillaOutflowInput
                        {
                            MovementType = CashMovementType.ProviderPayment,
                            Amount = 40m,
                            PurchaseId = Guid.NewGuid(),
                        },
                        new PlanillaOutflowInput
                        {
                            MovementType = CashMovementType.PettyCash,
                            Amount = 10m,
                        },
                        new PlanillaOutflowInput
                        {
                            MovementType = CashMovementType.OtherOutflow,
                            Amount = 5m,
                        },
                    ],
                },
            ]);

        Assert.Equal("Planilla", model.Title);
        Assert.Equal(70m, model.TotalSales);
        Assert.Equal(30m, model.VitrinaCafeSales);
        Assert.Equal(100m, model.CollectedPayments);
        Assert.Equal(55m, model.TotalEgresos);
        Assert.Equal(40m, model.TotalPurchasesPaid);
        Assert.Equal(-10m, model.OverShort);
        Assert.Equal(2, model.Categories.Count);
        Assert.Contains(model.Categories, c => c.ProductTypeName == "Vitrina Café" && c.Quantity == 2m);
        Assert.DoesNotContain(model.Categories, c => c.LineTotal == 0 && c.Quantity == 0);
        Assert.Equal(3, model.EgresosByType.Count);
        Assert.Single(model.Shifts);
        Assert.Equal(-10m, model.Shifts[0].OverShort);
    }

    [Fact]
    public void Build_keeps_vitrina_inside_ventas_when_flag_is_off()
    {
        var model = PlanillaDocumentFactory.Build(
            "Otro",
            new DateOnly(2026, 11, 6),
            isDailyPlanilla: false,
            separateVitrina: false,
            dayNotes: null,
            [
                new PlanillaShiftInput
                {
                    CashierEmail = "cajero@test.local",
                    OpenedAtUtc = DateTime.UtcNow,
                    Payments =
                    [
                        new PlanillaPaymentInput
                        {
                            BillId = Guid.NewGuid(),
                            Amount = 100m,
                            Method = PaymentMethod.Card,
                        },
                    ],
                    BillLines =
                    [
                        new PlanillaBillLineInput
                        {
                            BillId = Guid.NewGuid(),
                            ProductTypeName = "Vitrina Café",
                            Quantity = 1,
                            LineTotal = 30m,
                        },
                    ],
                },
            ]);

        Assert.Equal("Cierre de turno", model.Title);
        Assert.Equal(100m, model.TotalSales);
        Assert.Equal(0m, model.VitrinaCafeSales);
        Assert.Equal("Tarjeta", model.PaymentsByMethod.Single().MethodLabel);
    }
}
