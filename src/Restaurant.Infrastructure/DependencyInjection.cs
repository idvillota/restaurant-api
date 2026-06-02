using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Infrastructure.Common;
using Restaurant.Infrastructure.Identity;
using Restaurant.Infrastructure.Persistence;
using Restaurant.Application.Common.Options;
using Restaurant.Infrastructure.Services;
using Restaurant.Infrastructure.Storage;

namespace Restaurant.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<ICurrentTenantContext, CurrentTenantContext>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'ConnectionStrings:DefaultConnection' is not configured.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITenantUserInviteService, TenantUserInviteService>();
        services.Configure<ProductImageOptions>(configuration.GetSection(ProductImageOptions.SectionName));
        services.Configure<KitchenTicketOptions>(configuration.GetSection(KitchenTicketOptions.SectionName));
        services.Configure<SalesReceiptOptions>(configuration.GetSection(SalesReceiptOptions.SectionName));
        services.Configure<GeminiOptions>(configuration.GetSection(GeminiOptions.SectionName));
        services.AddHttpClient(nameof(StrategicAiReportService), client =>
        {
            client.Timeout = TimeSpan.FromMinutes(3);
        });
        services.AddScoped<IProductImageStorage, LocalProductImageStorage>();
        services.AddScoped<IKitchenTicketService, KitchenTicketService>();
        services.AddScoped<ISalesReceiptService, SalesReceiptService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IProductTypeService, ProductTypeService>();
        services.AddScoped<IProviderService, ProviderService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IDiningTableService, DiningTableService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IReservationService, ReservationService>();
        services.AddScoped<IIngredientCategoryService, IngredientCategoryService>();
        services.AddScoped<IIngredientService, IngredientService>();
        services.AddScoped<IPurchaseService, PurchaseService>();
        services.AddScoped<ISalesOrderService, SalesOrderService>();
        services.AddScoped<IBillService, BillService>();
        services.AddScoped<ITenantSettingsService, TenantSettingsService>();
        services.AddScoped<IOperationalBusinessDayService, OperationalBusinessDayService>();
        services.AddScoped<ICashierShiftService, CashierShiftService>();
        services.AddScoped<IDailyClosureService, DailyClosureService>();
        services.AddScoped<IInventoryAvailabilityService, InventoryAvailabilityService>();
        services.AddScoped<IRolePermissionService, RolePermissionService>();
        services.AddScoped<IStrategicAiReportService, StrategicAiReportService>();
        services.AddScoped<IPublicMenuService, PublicMenuService>();

        return services;
    }
}
