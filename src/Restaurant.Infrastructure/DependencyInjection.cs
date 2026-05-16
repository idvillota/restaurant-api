using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Infrastructure.Common;
using Restaurant.Infrastructure.Identity;
using Restaurant.Infrastructure.Persistence;
using Restaurant.Infrastructure.Services;

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
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IProductTypeService, ProductTypeService>();
        services.AddScoped<IProviderService, ProviderService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IDiningTableService, DiningTableService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IReservationService, ReservationService>();
        services.AddScoped<IIngredientCategoryService, IngredientCategoryService>();
        services.AddScoped<IIngredientService, IngredientService>();

        return services;
    }
}
