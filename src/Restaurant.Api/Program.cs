using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Restaurant.Api.Middleware;
using Restaurant.Application;
using Restaurant.Api.Authorization;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Common.Options;
using Restaurant.Infrastructure;
using Restaurant.Infrastructure.Identity;
using Restaurant.Infrastructure.Persistence;
using Restaurant.Infrastructure.Persistence.Seeding;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var jwtSection = builder.Configuration.GetSection(JwtSettings.SectionName);
var jwtSettings = jwtSection.Get<JwtSettings>()
    ?? throw new InvalidOperationException($"Missing configuration section '{JwtSettings.SectionName}'.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
        };
    });

// Secured controllers use [RequireFeature]; public endpoints use [AllowAnonymous].
builder.Services.AddFeatureAuthorization();
builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Restaurant Management API",
        Version = "v1",
        Description = "Multi-tenant restaurant management system API"
    });

    options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT Authorization header using the Bearer scheme."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("bearer", document)] = []
    });
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                      ?? ["http://localhost:5173"];
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>(tags: ["db", "ready"]);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();

    var permissionLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await PermissionBootstrap.EnsureAsync(db, permissionLogger, CancellationToken.None);

    if (app.Environment.IsDevelopment())
    {
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ICurrentTenantContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var productImages = scope.ServiceProvider.GetRequiredService<IProductImageStorage>();
        await DevelopmentDataSeeder.SeedAsync(
            db,
            passwordHasher,
            logger,
            tenantContext,
            app.Environment,
            productImages);

        await DevelopmentHistoricalDataSeeder.SeedAsync(db, logger, tenantContext, CancellationToken.None);
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Restaurant Management API v1");
        options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
    });
}

if (!app.Environment.IsDevelopment())
{
    app.UseForwardedHeaders();
    app.UseHttpsRedirection();
    app.UseHsts();
}

app.UseCors();
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();

var productImageOptions = app.Configuration.GetSection(ProductImageOptions.SectionName).Get<ProductImageOptions>()
    ?? new ProductImageOptions();
var productImageRoot = Path.IsPathRooted(productImageOptions.RootPath)
    ? productImageOptions.RootPath
    : Path.Combine(app.Environment.ContentRootPath, productImageOptions.RootPath);
Directory.CreateDirectory(productImageRoot);

var kitchenTicketOptions = app.Configuration.GetSection(KitchenTicketOptions.SectionName).Get<KitchenTicketOptions>()
    ?? new KitchenTicketOptions();
var kitchenTicketRoot = Path.IsPathRooted(kitchenTicketOptions.RootPath)
    ? kitchenTicketOptions.RootPath
    : Path.Combine(app.Environment.ContentRootPath, kitchenTicketOptions.RootPath);
Directory.CreateDirectory(kitchenTicketRoot);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(productImageRoot),
    RequestPath = productImageOptions.PublicBasePath.TrimEnd('/'),
});

app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();
