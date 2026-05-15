using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Infrastructure.Common;

namespace Restaurant.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var infrastructureProjectRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        var apiProjectRoot = Path.GetFullPath(Path.Combine(infrastructureProjectRoot, "..", "Restaurant.Api"));

        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiProjectRoot)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                $"Connection string 'DefaultConnection' not found. Expected appsettings under: {apiProjectRoot}");

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        ICurrentTenantContext tenant = new CurrentTenantContext();
        return new ApplicationDbContext(options, tenant);
    }
}
