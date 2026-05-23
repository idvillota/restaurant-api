using System.Reflection;

namespace Restaurant.Infrastructure.Persistence.Seeding;

/// <summary>
/// Stable identifiers so relationships stay consistent across seed runs.
/// </summary>
internal static class DevelopmentSeedIds
{
    static DevelopmentSeedIds() => ValidateAllGuids();

    private static void ValidateAllGuids()
    {
        foreach (var field in typeof(DevelopmentSeedIds).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.FieldType == typeof(Guid))
            {
                _ = (Guid)field.GetValue(null)!;
                continue;
            }

            if (field.FieldType == typeof(Guid[]))
            {
                foreach (var id in (Guid[])field.GetValue(null)!)
                    _ = id;
            }
        }
    }
    public const string TenantSlug = "demo-bistro";
    public const string AdminEmail = "owner@demo-bistro.local";
    public const string DemoTestEmail = "idvillota@gmail.com";
    public const string ManagerEmail = "manager@demo-bistro.local";
    public const string WaitressEmail = "mesera@demo-bistro.local";
    public const string CashierEmail = "cajero@demo-bistro.local";
    public const string AdminPassword = "Demo123!";

    public static readonly Guid TenantId = Guid.Parse("a1000001-0001-4001-8001-000000000001");
    public static readonly Guid AdminUserId = Guid.Parse("a1000002-0002-4002-8002-000000000002");
    public static readonly Guid AdminTenantUserId = Guid.Parse("a1000003-0003-4003-8003-000000000003");
    public static readonly Guid DemoTestUserId = Guid.Parse("a1000008-0008-4008-8008-000000000008");
    public static readonly Guid DemoTestTenantUserId = Guid.Parse("a1000009-0009-4009-8009-000000000009");
    public static readonly Guid AdministratorRoleId = Guid.Parse("a1000004-0004-4004-8004-000000000004");
    public static readonly Guid ManagerRoleId = Guid.Parse("a1000005-0005-4005-8005-000000000005");
    public static readonly Guid WaitressRoleId = Guid.Parse("a1000006-0006-4006-8006-000000000006");
    public static readonly Guid CashierRoleId = Guid.Parse("a1000011-0017-4017-8017-000000000011");
    public static readonly Guid ManagerUserId = Guid.Parse("a100000b-0011-4011-8011-00000000000b");
    public static readonly Guid WaitressUserId = Guid.Parse("a100000c-0012-4012-8012-00000000000c");
    public static readonly Guid CashierUserId = Guid.Parse("a100000d-0013-4013-8013-00000000000d");
    public static readonly Guid ManagerTenantUserId = Guid.Parse("a100000e-0014-4014-8014-00000000000e");
    public static readonly Guid WaitressTenantUserId = Guid.Parse("a100000f-0015-4015-8015-00000000000f");
    public static readonly Guid CashierTenantUserId = Guid.Parse("a1000010-0016-4016-8016-000000000010");

    public static readonly Guid[] IngredientCategoryIds =
    [
        Guid.Parse("b1000001-0001-4001-8001-000000000001"),
        Guid.Parse("b1000002-0002-4002-8002-000000000002"),
        Guid.Parse("b1000003-0003-4003-8003-000000000003"),
        Guid.Parse("b1000004-0004-4004-8004-000000000004"),
        Guid.Parse("b1000005-0005-4005-8005-000000000005"),
        Guid.Parse("b1000006-0006-4006-8006-000000000006"),
        Guid.Parse("b1000007-0007-4007-8007-000000000007"),
        Guid.Parse("b1000008-0008-4008-8008-000000000008"),
        Guid.Parse("b1000009-0009-4009-8009-000000000009"),
        Guid.Parse("b1000010-0010-4010-8010-000000000010"),
    ];

    public static readonly Guid[] IngredientIds =
    [
        Guid.Parse("c1000001-0001-4001-8001-000000000001"),
        Guid.Parse("c1000002-0002-4002-8002-000000000002"),
        Guid.Parse("c1000003-0003-4003-8003-000000000003"),
        Guid.Parse("c1000004-0004-4004-8004-000000000004"),
        Guid.Parse("c1000005-0005-4005-8005-000000000005"),
        Guid.Parse("c1000006-0006-4006-8006-000000000006"),
        Guid.Parse("c1000007-0007-4007-8007-000000000007"),
        Guid.Parse("c1000008-0008-4008-8008-000000000008"),
        Guid.Parse("c1000009-0009-4009-8009-000000000009"),
        Guid.Parse("c1000010-0010-4010-8010-000000000010"),
        Guid.Parse("c1000011-0011-4011-8011-000000000011"),
        Guid.Parse("c1000012-0012-4012-8012-000000000012"),
    ];

    public static readonly Guid[] ProviderIds =
    [
        Guid.Parse("d1000001-0001-4001-8001-000000000001"),
        Guid.Parse("d1000002-0002-4002-8002-000000000002"),
        Guid.Parse("d1000003-0003-4003-8003-000000000003"),
        Guid.Parse("d1000004-0004-4004-8004-000000000004"),
        Guid.Parse("d1000005-0005-4005-8005-000000000005"),
        Guid.Parse("d1000006-0006-4006-8006-000000000006"),
        Guid.Parse("d1000007-0007-4007-8007-000000000007"),
        Guid.Parse("d1000008-0008-4008-8008-000000000008"),
        Guid.Parse("d1000009-0009-4009-8009-000000000009"),
        Guid.Parse("d1000010-0010-4010-8010-000000000010"),
    ];

    public static readonly Guid[] ProductTypeIds =
    [
        Guid.Parse("e1000001-0001-4001-8001-000000000001"),
        Guid.Parse("e1000002-0002-4002-8002-000000000002"),
        Guid.Parse("e1000003-0003-4003-8003-000000000003"),
        Guid.Parse("e1000004-0004-4004-8004-000000000004"),
        Guid.Parse("e1000005-0005-4005-8005-000000000005"),
        Guid.Parse("e1000006-0006-4006-8006-000000000006"),
        Guid.Parse("e1000007-0007-4007-8007-000000000007"),
        Guid.Parse("e1000008-0008-4008-8008-000000000008"),
        Guid.Parse("e1000009-0009-4009-8009-000000000009"),
        Guid.Parse("e1000010-0010-4010-8010-000000000010"),
    ];

    public static readonly Guid[] ProductIds =
    [
        Guid.Parse("f1000001-0001-4001-8001-000000000001"),
        Guid.Parse("f1000002-0002-4002-8002-000000000002"),
        Guid.Parse("f1000003-0003-4003-8003-000000000003"),
        Guid.Parse("f1000004-0004-4004-8004-000000000004"),
        Guid.Parse("f1000005-0005-4005-8005-000000000005"),
        Guid.Parse("f1000006-0006-4006-8006-000000000006"),
        Guid.Parse("f1000007-0007-4007-8007-000000000007"),
        Guid.Parse("f1000008-0008-4008-8008-000000000008"),
        Guid.Parse("f1000009-0009-4009-8009-000000000009"),
        Guid.Parse("f1000010-0010-4010-8010-000000000010"),
        Guid.Parse("f1000011-0011-4011-8011-000000000011"),
        Guid.Parse("f1000012-0012-4012-8012-000000000012"),
    ];

    public static readonly Guid[] DiningTableIds =
    [
        Guid.Parse("10000001-0001-4001-8001-000000000001"),
        Guid.Parse("10000002-0002-4002-8002-000000000002"),
        Guid.Parse("10000003-0003-4003-8003-000000000003"),
        Guid.Parse("10000004-0004-4004-8004-000000000004"),
        Guid.Parse("10000005-0005-4005-8005-000000000005"),
        Guid.Parse("10000006-0006-4006-8006-000000000006"),
        Guid.Parse("10000007-0007-4007-8007-000000000007"),
        Guid.Parse("10000008-0008-4008-8008-000000000008"),
        Guid.Parse("10000009-0009-4009-8009-000000000009"),
        Guid.Parse("1000000a-0010-4010-8010-00000000000a"),
        Guid.Parse("1000000b-0011-4011-8011-00000000000b"),
        Guid.Parse("1000000c-0012-4012-8012-00000000000c"),
    ];

    public static readonly Guid[] CustomerIds =
    [
        Guid.Parse("11000001-0001-4001-8001-000000000001"),
        Guid.Parse("11000002-0002-4002-8002-000000000002"),
        Guid.Parse("11000003-0003-4003-8003-000000000003"),
        Guid.Parse("11000004-0004-4004-8004-000000000004"),
        Guid.Parse("11000005-0005-4005-8005-000000000005"),
        Guid.Parse("11000006-0006-4006-8006-000000000006"),
        Guid.Parse("11000007-0007-4007-8007-000000000007"),
        Guid.Parse("11000008-0008-4008-8008-000000000008"),
        Guid.Parse("11000009-0009-4009-8009-000000000009"),
        Guid.Parse("1100000a-0010-4010-8010-00000000000a"),
    ];

    public static readonly Guid[] PurchaseIds =
    [
        Guid.Parse("12000001-0001-4001-8001-000000000001"),
        Guid.Parse("12000002-0002-4002-8002-000000000002"),
        Guid.Parse("12000003-0003-4003-8003-000000000003"),
        Guid.Parse("12000004-0004-4004-8004-000000000004"),
        Guid.Parse("12000005-0005-4005-8005-000000000005"),
        Guid.Parse("12000006-0006-4006-8006-000000000006"),
        Guid.Parse("12000007-0007-4007-8007-000000000007"),
        Guid.Parse("12000008-0008-4008-8008-000000000008"),
        Guid.Parse("12000009-0009-4009-8009-000000000009"),
        Guid.Parse("1200000a-0010-4010-8010-00000000000a"),
    ];

    public static readonly Guid[] ReservationIds =
    [
        Guid.Parse("13000001-0001-4001-8001-000000000001"),
        Guid.Parse("13000002-0002-4002-8002-000000000002"),
        Guid.Parse("13000003-0003-4003-8003-000000000003"),
        Guid.Parse("13000004-0004-4004-8004-000000000004"),
        Guid.Parse("13000005-0005-4005-8005-000000000005"),
        Guid.Parse("13000006-0006-4006-8006-000000000006"),
        Guid.Parse("13000007-0007-4007-8007-000000000007"),
        Guid.Parse("13000008-0008-4008-8008-000000000008"),
        Guid.Parse("13000009-0009-4009-8009-000000000009"),
        Guid.Parse("1300000a-0010-4010-8010-00000000000a"),
    ];

    public static readonly Guid[] SalesOrderIds =
    [
        Guid.Parse("14000001-0001-4001-8001-000000000001"),
        Guid.Parse("14000002-0002-4002-8002-000000000002"),
        Guid.Parse("14000003-0003-4003-8003-000000000003"),
        Guid.Parse("14000004-0004-4004-8004-000000000004"),
        Guid.Parse("14000005-0005-4005-8005-000000000005"),
        Guid.Parse("14000006-0006-4006-8006-000000000006"),
        Guid.Parse("14000007-0007-4007-8007-000000000007"),
        Guid.Parse("14000008-0008-4008-8008-000000000008"),
        Guid.Parse("14000009-0009-4009-8009-000000000009"),
        Guid.Parse("1400000a-0010-4010-8010-00000000000a"),
    ];

    public static readonly Guid[] InvoiceIds =
    [
        Guid.Parse("15000001-0001-4001-8001-000000000001"),
        Guid.Parse("15000002-0002-4002-8002-000000000002"),
        Guid.Parse("15000003-0003-4003-8003-000000000003"),
        Guid.Parse("15000004-0004-4004-8004-000000000004"),
        Guid.Parse("15000005-0005-4005-8005-000000000005"),
        Guid.Parse("15000006-0006-4006-8006-000000000006"),
        Guid.Parse("15000007-0007-4007-8007-000000000007"),
        Guid.Parse("15000008-0008-4008-8008-000000000008"),
        Guid.Parse("15000009-0009-4009-8009-000000000009"),
        Guid.Parse("1500000a-0010-4010-8010-00000000000a"),
    ];

    public static readonly Guid[] BillIds =
    [
        Guid.Parse("18000001-0001-4001-8001-000000000001"),
        Guid.Parse("18000002-0002-4002-8002-000000000002"),
        Guid.Parse("18000003-0003-4003-8003-000000000003"),
    ];

    public static readonly Guid[] PaymentIds =
    [
        Guid.Parse("16000001-0001-4001-8001-000000000001"),
        Guid.Parse("16000002-0002-4002-8002-000000000002"),
        Guid.Parse("16000003-0003-4003-8003-000000000003"),
        Guid.Parse("16000004-0004-4004-8004-000000000004"),
        Guid.Parse("16000005-0005-4005-8005-000000000005"),
        Guid.Parse("16000006-0006-4006-8006-000000000006"),
        Guid.Parse("16000007-0007-4007-8007-000000000007"),
        Guid.Parse("16000008-0008-4008-8008-000000000008"),
        Guid.Parse("16000009-0009-4009-8009-000000000009"),
        Guid.Parse("1600000a-0010-4010-8010-00000000000a"),
    ];

    public static readonly Guid[] EmployeeIds =
    [
        Guid.Parse("17000001-0001-4001-8001-000000000001"),
        Guid.Parse("17000002-0002-4002-8002-000000000002"),
        Guid.Parse("17000003-0003-4003-8003-000000000003"),
        Guid.Parse("17000004-0004-4004-8004-000000000004"),
        Guid.Parse("17000005-0005-4005-8005-000000000005"),
        Guid.Parse("17000006-0006-4006-8006-000000000006"),
        Guid.Parse("17000007-0007-4007-8007-000000000007"),
        Guid.Parse("17000008-0008-4008-8008-000000000008"),
        Guid.Parse("17000009-0009-4009-8009-000000000009"),
        Guid.Parse("1700000a-0010-4010-8010-00000000000a"),
    ];
}
