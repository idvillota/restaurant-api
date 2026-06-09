using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
// Azure OpenAI integration uses HttpClient; avoid direct dependency on Azure SDK in DI file.
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
        services.Configure<AzureOpenAiOptions>(configuration.GetSection(AzureOpenAiOptions.SectionName));
        services.AddHttpClient(nameof(StrategicAiReportService), client =>
        {
            client.Timeout = TimeSpan.FromMinutes(3);
        });

        // Configure a named HttpClient for Azure OpenAI interaction. BaseAddress and api-key header are set from configuration at runtime.
        services.AddHttpClient("AzureOpenAiClient", client =>
        {
            var azureSectionLocal = configuration.GetSection(AzureOpenAiOptions.SectionName);
            var endpointLocal = azureSectionLocal.GetValue<string>(nameof(AzureOpenAiOptions.Endpoint));
            var apiKeyLocal = azureSectionLocal.GetValue<string>(nameof(AzureOpenAiOptions.ApiKey));
            if (!string.IsNullOrWhiteSpace(endpointLocal))
            {
                var trimmed = endpointLocal.TrimEnd('/');
                client.BaseAddress = new Uri(trimmed);
            }

            if (!string.IsNullOrWhiteSpace(apiKeyLocal))
            {
                // Azure OpenAI expects the header 'api-key' for key-based auth
                if (!client.DefaultRequestHeaders.Contains("api-key"))
                    client.DefaultRequestHeaders.Add("api-key", apiKeyLocal);
            }

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
        // Register strategic AI report service implementation.
        // Prefer AzureOpenAi if configured; otherwise fall back to existing Gemini-based service.
        var azureSection = configuration.GetSection(AzureOpenAiOptions.SectionName);
        var azureEndpoint = azureSection.GetValue<string>(nameof(AzureOpenAiOptions.Endpoint));

        if (!string.IsNullOrWhiteSpace(azureEndpoint))
        {
            // AzureOpenAi configured: use AzureStrategicAiReportService which uses the named HttpClient 'AzureOpenAiClient'.
            services.AddScoped<IStrategicAiReportService, AzureStrategicAiReportService>();
        }
        else
        {
            services.AddScoped<IStrategicAiReportService, StrategicAiReportService>();
        }
        services.AddScoped<IOperationalReportsService, OperationalReportsService>();
        services.AddScoped<IPublicMenuService, PublicMenuService>();

        return services;
    }
}
