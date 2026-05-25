using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Domain.Common;
using Restaurant.Domain.Entities;

namespace Restaurant.Infrastructure.Persistence;

public sealed class ApplicationDbContext : DbContext
{
    private readonly ICurrentTenantContext _currentTenant;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICurrentTenantContext currentTenant)
        : base(options)
    {
        _currentTenant = currentTenant;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<TenantUser> TenantUsers => Set<TenantUser>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Feature> Features => Set<Feature>();
    public DbSet<RoleFeature> RoleFeatures => Set<RoleFeature>();
    public DbSet<TenantUserRole> TenantUserRoles => Set<TenantUserRole>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<ProductType> ProductTypes => Set<ProductType>();
    public DbSet<IngredientCategory> IngredientCategories => Set<IngredientCategory>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<ProductIngredient> ProductIngredients => Set<ProductIngredient>();
    public DbSet<Provider> Providers => Set<Provider>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseLine> PurchaseLines => Set<PurchaseLine>();
    public DbSet<DiningTable> DiningTables => Set<DiningTable>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<ReservationTable> ReservationTables => Set<ReservationTable>();
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<SalesOrderLine> SalesOrderLines => Set<SalesOrderLine>();
    public DbSet<SalesOrderLineExcludedIngredient> SalesOrderLineExcludedIngredients =>
        Set<SalesOrderLineExcludedIngredient>();
    public DbSet<TenantSettings> TenantSettings => Set<TenantSettings>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<BillLine> BillLines => Set<BillLine>();
    public DbSet<BillSalesOrder> BillSalesOrders => Set<BillSalesOrder>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<CashierShift> CashierShifts => Set<CashierShift>();
    public DbSet<DailyClosure> DailyClosures => Set<DailyClosure>();
    public DbSet<CashMovement> CashMovements => Set<CashMovement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(x => x.NormalizedEmail).IsUnique();
            e.Property(x => x.Email).HasMaxLength(320);
            e.Property(x => x.NormalizedEmail).HasMaxLength(320);
        });

        modelBuilder.Entity<Tenant>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Slug).HasMaxLength(120);
            e.Property(x => x.CurrencyCode).HasMaxLength(8);
        });

        modelBuilder.Entity<TenantUser>(e =>
        {
            e.HasOne(x => x.Tenant).WithMany(x => x.TenantUsers).HasForeignKey(x => x.TenantId);
            e.HasOne(x => x.User).WithMany(x => x.TenantUsers).HasForeignKey(x => x.UserId);
            e.HasIndex(x => new { x.TenantId, x.UserId }).IsUnique();
        });

        modelBuilder.Entity<Role>(e =>
        {
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
            e.HasIndex(x => new { x.TenantId, x.NormalizedName }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(120);
            e.Property(x => x.NormalizedName).HasMaxLength(120);
        });

        modelBuilder.Entity<TenantUserRole>(e =>
        {
            e.HasOne(x => x.TenantUser).WithMany(x => x.TenantUserRoles).HasForeignKey(x => x.TenantUserId);
            e.HasOne(x => x.Role).WithMany(x => x.TenantUserRoles).HasForeignKey(x => x.RoleId);
            e.HasIndex(x => new { x.TenantUserId, x.RoleId }).IsUnique();
        });

        modelBuilder.Entity<Feature>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(80);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Module).HasMaxLength(80);
        });

        modelBuilder.Entity<RoleFeature>(e =>
        {
            e.HasOne(x => x.Role).WithMany(x => x.RoleFeatures).HasForeignKey(x => x.RoleId);
            e.HasOne(x => x.Feature).WithMany(x => x.RoleFeatures).HasForeignKey(x => x.FeatureId);
            e.HasIndex(x => new { x.RoleId, x.FeatureId }).IsUnique();
        });

        modelBuilder.Entity<Employee>(e =>
        {
            e.HasOne(x => x.TenantUser).WithOne(x => x.Employee).HasForeignKey<Employee>(x => x.TenantUserId);
        });

        modelBuilder.Entity<ProductType>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
        });

        modelBuilder.Entity<IngredientCategory>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.HasOne(x => x.ProductType).WithMany(x => x.Products).HasForeignKey(x => x.ProductTypeId);
            e.Property(x => x.UnitPrice).HasPrecision(18, 2);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Sku).HasMaxLength(80);
            e.Property(x => x.ImagePath).HasMaxLength(260);
        });

        modelBuilder.Entity<Ingredient>(e =>
        {
            e.HasOne(x => x.IngredientCategory)
                .WithMany(x => x.Ingredients)
                .HasForeignKey(x => x.IngredientCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.UnitCost).HasPrecision(18, 4);
            e.Property(x => x.StockQuantity).HasPrecision(18, 4);
            e.Property(x => x.ReorderLevel).HasPrecision(18, 4);
        });

        modelBuilder.Entity<ProductIngredient>(e =>
        {
            e.HasOne(x => x.Product).WithMany(x => x.ProductIngredients).HasForeignKey(x => x.ProductId);
            e.HasOne(x => x.Ingredient).WithMany(x => x.ProductIngredients).HasForeignKey(x => x.IngredientId);
            e.HasIndex(x => new { x.ProductId, x.IngredientId }).IsUnique();
            e.Property(x => x.Quantity).HasPrecision(18, 4);
        });

        modelBuilder.Entity<Provider>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.ContactName).HasMaxLength(200);
            e.Property(x => x.Address).HasMaxLength(500);
        });

        modelBuilder.Entity<Purchase>(e =>
        {
            e.HasOne(x => x.Provider).WithMany().HasForeignKey(x => x.ProviderId);
            e.Property(x => x.BillNumber).HasMaxLength(80);
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.Property(x => x.Subtotal).HasPrecision(18, 2);
            e.Property(x => x.TaxAmount).HasPrecision(18, 2);
            e.Property(x => x.Total).HasPrecision(18, 2);
            e.HasIndex(x => new { x.TenantId, x.BillNumber }).IsUnique();
        });

        modelBuilder.Entity<PurchaseLine>(e =>
        {
            e.HasOne(x => x.Purchase).WithMany(x => x.Lines).HasForeignKey(x => x.PurchaseId);
            e.HasOne(x => x.Ingredient).WithMany().HasForeignKey(x => x.IngredientId);
            e.Property(x => x.Quantity).HasPrecision(18, 4);
            e.Property(x => x.UnitPrice).HasPrecision(18, 4);
            e.Property(x => x.LineTotal).HasPrecision(18, 2);
            e.HasIndex(x => new { x.PurchaseId, x.IngredientId }).IsUnique();
        });

        modelBuilder.Entity<DiningTable>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(40);
            e.Property(x => x.Zone).HasMaxLength(80);
        });

        modelBuilder.Entity<Reservation>(e =>
        {
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
            e.Property(x => x.ContactName).HasMaxLength(200);
        });

        modelBuilder.Entity<ReservationTable>(e =>
        {
            e.HasOne(x => x.Reservation).WithMany(x => x.ReservationTables).HasForeignKey(x => x.ReservationId);
            e.HasOne(x => x.DiningTable).WithMany(x => x.ReservationTables).HasForeignKey(x => x.DiningTableId);
            e.HasIndex(x => new { x.ReservationId, x.DiningTableId }).IsUnique();
        });

        modelBuilder.Entity<SalesOrder>(e =>
        {
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
            e.HasOne(x => x.DiningTable).WithMany().HasForeignKey(x => x.DiningTableId);
            e.Property(x => x.Number).HasMaxLength(40);
            e.Property(x => x.Subtotal).HasPrecision(18, 2);
            e.Property(x => x.TaxAmount).HasPrecision(18, 2);
            e.Property(x => x.Total).HasPrecision(18, 2);
            e.HasIndex(x => new { x.TenantId, x.DiningTableId, x.Status });
        });

        modelBuilder.Entity<SalesOrderLine>(e =>
        {
            e.HasOne(x => x.SalesOrder).WithMany(x => x.Lines).HasForeignKey(x => x.SalesOrderId);
            e.HasOne(x => x.Product).WithMany(x => x.SalesOrderLines).HasForeignKey(x => x.ProductId);
            e.Property(x => x.UnitPrice).HasPrecision(18, 2);
            e.Property(x => x.LineTotal).HasPrecision(18, 2);
            e.Property(x => x.Quantity).HasPrecision(18, 4);
            e.Property(x => x.Notes).HasMaxLength(500);
        });

        modelBuilder.Entity<SalesOrderLineExcludedIngredient>(e =>
        {
            e.HasOne(x => x.SalesOrderLine)
                .WithMany(x => x.ExcludedIngredients)
                .HasForeignKey(x => x.SalesOrderLineId);
            e.HasOne(x => x.Ingredient).WithMany().HasForeignKey(x => x.IngredientId);
            e.HasIndex(x => new { x.SalesOrderLineId, x.IngredientId }).IsUnique();
        });

        modelBuilder.Entity<TenantSettings>(e =>
        {
            e.HasKey(x => x.TenantId);
            e.HasOne(x => x.Tenant).WithOne().HasForeignKey<TenantSettings>(x => x.TenantId);
            e.Property(x => x.MaxDiscountPercent).HasPrecision(5, 2);
            e.Property(x => x.ImpoconsumoPercent).HasPrecision(5, 2);
            e.Property(x => x.TradeName).HasMaxLength(200);
            e.Property(x => x.LegalName).HasMaxLength(200);
            e.Property(x => x.TaxRegime).HasMaxLength(80);
            e.Property(x => x.TaxId).HasMaxLength(40);
            e.Property(x => x.LegalRepresentative).HasMaxLength(200);
            e.Property(x => x.AddressLine).HasMaxLength(300);
            e.Property(x => x.City).HasMaxLength(120);
            e.Property(x => x.Country).HasMaxLength(80);
            e.Property(x => x.PostalCode).HasMaxLength(20);
            e.Property(x => x.Phone).HasMaxLength(40);
            e.Property(x => x.DianResolutionNumber).HasMaxLength(80);
            e.Property(x => x.InvoiceNumberPrefix).HasMaxLength(20);
        });

        modelBuilder.Entity<CashierShift>(e =>
        {
            e.HasOne(x => x.CashierUser).WithMany().HasForeignKey(x => x.CashierUserId);
            e.Property(x => x.OpeningFloat).HasPrecision(18, 2);
            e.Property(x => x.ExpectedCash).HasPrecision(18, 2);
            e.Property(x => x.CountedCash).HasPrecision(18, 2);
            e.Property(x => x.ClosingNotes).HasMaxLength(500);
            e.HasIndex(x => new { x.TenantId, x.CashierUserId, x.Status });
            e.HasIndex(x => new { x.TenantId, x.BusinessDate });
        });

        modelBuilder.Entity<DailyClosure>(e =>
        {
            e.HasOne(x => x.ClosedByUser).WithMany().HasForeignKey(x => x.ClosedByUserId);
            e.Property(x => x.Notes).HasMaxLength(500);
            e.HasIndex(x => new { x.TenantId, x.BusinessDate }).IsUnique();
        });

        modelBuilder.Entity<CashMovement>(e =>
        {
            e.HasOne(x => x.CashierShift).WithMany(x => x.CashMovements).HasForeignKey(x => x.CashierShiftId);
            e.HasOne(x => x.Purchase).WithMany().HasForeignKey(x => x.PurchaseId);
            e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId);
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Description).HasMaxLength(300);
            e.HasIndex(x => new { x.TenantId, x.BusinessDate });
        });

        modelBuilder.Entity<Bill>(e =>
        {
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
            e.HasOne(x => x.CashierShift).WithMany().HasForeignKey(x => x.CashierShiftId);
            e.Property(x => x.Number).HasMaxLength(40);
            e.Property(x => x.ProcessedByDisplayName).HasMaxLength(200);
            e.Property(x => x.TableCodesSnapshot).HasMaxLength(200);
            e.Property(x => x.OrderNumbersSnapshot).HasMaxLength(500);
            e.Property(x => x.ReceiptPdfRelativePath).HasMaxLength(500);
            e.Property(x => x.ReceiptXmlRelativePath).HasMaxLength(500);
            e.Property(x => x.Subtotal).HasPrecision(18, 2);
            e.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            e.Property(x => x.DiscountPercent).HasPrecision(5, 2);
            e.Property(x => x.TipAmount).HasPrecision(18, 2);
            e.Property(x => x.TaxAmount).HasPrecision(18, 2);
            e.Property(x => x.Total).HasPrecision(18, 2);
            e.HasIndex(x => new { x.TenantId, x.Number }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.DianConsecutiveNumber })
                .IsUnique()
                .HasFilter("\"DianConsecutiveNumber\" > 0");
        });

        modelBuilder.Entity<BillLine>(e =>
        {
            e.HasOne(x => x.Bill).WithMany(x => x.Lines).HasForeignKey(x => x.BillId);
            e.Property(x => x.ProductName).HasMaxLength(200);
            e.Property(x => x.ProductTypeName).HasMaxLength(120);
            e.Property(x => x.Notes).HasMaxLength(500);
            e.Property(x => x.Quantity).HasPrecision(18, 4);
            e.Property(x => x.UnitPrice).HasPrecision(18, 2);
            e.Property(x => x.LineTotal).HasPrecision(18, 2);
            e.Property(x => x.ImpoconsumoAmount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<BillSalesOrder>(e =>
        {
            e.HasKey(x => new { x.BillId, x.SalesOrderId });
            e.HasOne(x => x.Bill).WithMany(x => x.BillOrders).HasForeignKey(x => x.BillId);
            e.HasOne(x => x.SalesOrder).WithMany().HasForeignKey(x => x.SalesOrderId);
            e.HasIndex(x => x.SalesOrderId).IsUnique();
        });

        modelBuilder.Entity<Invoice>(e =>
        {
            e.HasOne(x => x.Bill).WithOne(x => x.Invoice).HasForeignKey<Invoice>(x => x.BillId);
            e.HasOne(x => x.SalesOrder).WithMany(x => x.Invoices).HasForeignKey(x => x.SalesOrderId);
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
            e.Property(x => x.Number).HasMaxLength(40);
            e.Property(x => x.Subtotal).HasPrecision(18, 2);
            e.Property(x => x.TaxAmount).HasPrecision(18, 2);
            e.Property(x => x.Total).HasPrecision(18, 2);
            e.HasIndex(x => x.BillId).IsUnique();
        });

        modelBuilder.Entity<Payment>(e =>
        {
            e.HasOne(x => x.Bill).WithMany(x => x.Payments).HasForeignKey(x => x.BillId);
            e.HasOne(x => x.SalesOrder).WithMany(x => x.Payments).HasForeignKey(x => x.SalesOrderId);
            e.HasOne(x => x.Invoice).WithMany(x => x.Payments).HasForeignKey(x => x.InvoiceId);
            e.HasOne(x => x.CashierShift).WithMany(x => x.Payments).HasForeignKey(x => x.CashierShiftId);
            e.Property(x => x.Amount).HasPrecision(18, 2);
        });

        ApplyTenantFilters(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }

    private void ApplyTenantFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TenantUser>().HasQueryFilter(e =>
            _currentTenant.TenantId == null || e.TenantId == _currentTenant.TenantId);

        modelBuilder.Entity<Role>().HasQueryFilter(e =>
            _currentTenant.TenantId == null || e.TenantId == _currentTenant.TenantId);

        modelBuilder.Entity<RoleFeature>().HasQueryFilter(e =>
            _currentTenant.TenantId == null || e.TenantId == _currentTenant.TenantId);

        modelBuilder.Entity<TenantUserRole>().HasQueryFilter(e =>
            _currentTenant.TenantId == null || e.TenantId == _currentTenant.TenantId);

        modelBuilder.Entity<Employee>().HasQueryFilter(e =>
            _currentTenant.TenantId == null || e.TenantId == _currentTenant.TenantId);

        modelBuilder.Entity<ProductType>().HasQueryFilter(e =>
            _currentTenant.TenantId == null || e.TenantId == _currentTenant.TenantId);

        modelBuilder.Entity<Product>().HasQueryFilter(e =>
            _currentTenant.TenantId == null || e.TenantId == _currentTenant.TenantId);

        modelBuilder.Entity<IngredientCategory>().HasQueryFilter(e =>
            _currentTenant.TenantId == null || e.TenantId == _currentTenant.TenantId);

        modelBuilder.Entity<Ingredient>().HasQueryFilter(e =>
            _currentTenant.TenantId == null || e.TenantId == _currentTenant.TenantId);

        modelBuilder.Entity<ProductIngredient>().HasQueryFilter(e =>
            _currentTenant.TenantId == null || e.TenantId == _currentTenant.TenantId);

        modelBuilder.Entity<Provider>().HasQueryFilter(e =>
            _currentTenant.TenantId == null || e.TenantId == _currentTenant.TenantId);

        modelBuilder.Entity<Purchase>().HasQueryFilter(e =>
            _currentTenant.TenantId == null || e.TenantId == _currentTenant.TenantId);

        modelBuilder.Entity<PurchaseLine>().HasQueryFilter(e =>
            _currentTenant.TenantId == null || e.TenantId == _currentTenant.TenantId);

        modelBuilder.Entity<DiningTable>().HasQueryFilter(e =>
            _currentTenant.TenantId == null || e.TenantId == _currentTenant.TenantId);

        modelBuilder.Entity<Customer>().HasQueryFilter(e =>
            _currentTenant.TenantId == null || e.TenantId == _currentTenant.TenantId);

        modelBuilder.Entity<Reservation>().HasQueryFilter(e =>
            _currentTenant.TenantId == null || e.TenantId == _currentTenant.TenantId);

        modelBuilder.Entity<ReservationTable>().HasQueryFilter(e =>
            _currentTenant.TenantId == null || e.TenantId == _currentTenant.TenantId);

        modelBuilder.Entity<SalesOrder>().HasQueryFilter(e =>
            _currentTenant.TenantId == null || e.TenantId == _currentTenant.TenantId);

        modelBuilder.Entity<SalesOrderLine>().HasQueryFilter(e =>
            _currentTenant.TenantId == null || e.TenantId == _currentTenant.TenantId);

        modelBuilder.Entity<SalesOrderLineExcludedIngredient>().HasQueryFilter(e =>
            _currentTenant.TenantId == null || e.TenantId == _currentTenant.TenantId);

        modelBuilder.Entity<TenantSettings>().HasQueryFilter(e =>
            _currentTenant.TenantId == null || e.TenantId == _currentTenant.TenantId);

        modelBuilder.Entity<Bill>().HasQueryFilter(e =>
            _currentTenant.TenantId == null || e.TenantId == _currentTenant.TenantId);

        modelBuilder.Entity<BillLine>().HasQueryFilter(e =>
            _currentTenant.TenantId == null || e.TenantId == _currentTenant.TenantId);

        modelBuilder.Entity<BillSalesOrder>().HasQueryFilter(e =>
            _currentTenant.TenantId == null || e.TenantId == _currentTenant.TenantId);

        modelBuilder.Entity<Invoice>().HasQueryFilter(e =>
            _currentTenant.TenantId == null || e.TenantId == _currentTenant.TenantId);

        modelBuilder.Entity<Payment>().HasQueryFilter(e =>
            _currentTenant.TenantId == null || e.TenantId == _currentTenant.TenantId);

        modelBuilder.Entity<CashierShift>().HasQueryFilter(e =>
            _currentTenant.TenantId == null || e.TenantId == _currentTenant.TenantId);

        modelBuilder.Entity<DailyClosure>().HasQueryFilter(e =>
            _currentTenant.TenantId == null || e.TenantId == _currentTenant.TenantId);

        modelBuilder.Entity<CashMovement>().HasQueryFilter(e =>
            _currentTenant.TenantId == null || e.TenantId == _currentTenant.TenantId);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var utc = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<EntityBase>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAtUtc = utc;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAtUtc = utc;
                    break;
            }
        }

        foreach (var entry in ChangeTracker.Entries<ITenantScoped>())
        {
            if (entry.State == EntityState.Added && _currentTenant.TenantId.HasValue)
            {
                entry.Entity.TenantId = _currentTenant.TenantId.Value;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
