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
        services.AddScoped<IProductImageStorage, LocalProductImageStorage>();
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

        return services;
    }
}
