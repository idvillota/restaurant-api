using Microsoft.EntityFrameworkCore;
using Moq;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Auth;
using Restaurant.Application.Features.Organization.RolePermissions;
using Restaurant.Application.Authorization;
using Restaurant.Domain.Entities;
using Restaurant.Infrastructure.Authorization;
using Restaurant.Infrastructure.Identity;
using Restaurant.Infrastructure.Services;
using Restaurant.Tests.Unit.Support;

namespace Restaurant.Tests.Unit.Infrastructure.Services;

public sealed class AuthServiceTests
{
    private static async Task SeedFeaturesAsync(NoTenantDbFixture fx)
    {
        foreach (var def in FeatureCatalog.All)
        {
            await fx.Db.Features.AddAsync(new Feature
            {
                Id = def.Id,
                Code = def.Code,
                Name = def.Name,
                Module = def.Module,
                SortOrder = def.SortOrder,
            });
        }

        await fx.Db.SaveChangesAsync();
    }

    private static AuthService CreateSut(NoTenantDbFixture fx, Mock<IJwtTokenService>? jwt = null)
    {
        var useDefaultJwt = jwt is null;
        jwt ??= new Mock<IJwtTokenService>();
        if (useDefaultJwt)
        {
            jwt.Setup(j => j.CreateAccessToken(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyList<string>>(),
                    It.IsAny<IReadOnlyList<string>>()))
                .Returns("test-token");
        }

        var tenantContext = new Restaurant.Infrastructure.Common.CurrentTenantContext();
        IRolePermissionService rolePermissions = new RolePermissionService(fx.Db, tenantContext);
        var kitchenPrinters = new Mock<IKitchenPrinterService>();
        kitchenPrinters
            .Setup(s => s.EnsureDefaultStationAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return new AuthService(
            fx.UnitOfWork,
            fx.Db,
            new BcryptPasswordHasher(),
            jwt.Object,
            rolePermissions,
            tenantContext,
            kitchenPrinters.Object);
    }

    [Fact]
    public async Task RegisterTenantAsync_persists_tenant_user_roles_and_membership()
    {
        using var fx = new NoTenantDbFixture();
        await SeedFeaturesAsync(fx);
        var sut = CreateSut(fx);

        var result = await sut.RegisterTenantAsync(
            new RegisterTenantDto
            {
                TenantName = "Cafe Central",
                AdminEmail = "owner@example.com",
                Password = "Str0ng!Pass",
            });

        Assert.Equal("test-token", result.AccessToken);
        Assert.Equal("owner@example.com", result.Email);
        Assert.Single(await fx.Db.Tenants.ToListAsync());
        Assert.Single(await fx.Db.Users.ToListAsync());
        Assert.Equal(4, await fx.Db.Roles.CountAsync());
        Assert.Single(await fx.Db.TenantUsers.ToListAsync());
    }

    [Fact]
    public async Task RegisterTenantAsync_throws_when_email_already_used()
    {
        using var fx = new NoTenantDbFixture();
        await SeedFeaturesAsync(fx);
        var sut = CreateSut(fx);
        var dto = new RegisterTenantDto
        {
            TenantName = "First",
            AdminEmail = "dup@example.com",
            Password = "Str0ng!Pass",
        };
        await sut.RegisterTenantAsync(dto);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.RegisterTenantAsync(
                new RegisterTenantDto
                {
                    TenantName = "Second",
                    AdminEmail = "dup@example.com",
                    Password = "OtherPass1!",
                }));
    }

    [Fact]
    public async Task LoginAsync_after_register_returns_token()
    {
        using var fx = new NoTenantDbFixture();
        await SeedFeaturesAsync(fx);
        var jwt = new Mock<IJwtTokenService>();
        jwt.Setup(j => j.CreateAccessToken(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IReadOnlyList<string>>()))
            .Returns("jwt-value");
        var sut = CreateSut(fx, jwt);
        await sut.RegisterTenantAsync(
            new RegisterTenantDto
            {
                TenantName = "Login Cafe",
                AdminEmail = "chef@example.com",
                Password = "Secret123!",
            });

        var login = await sut.LoginAsync(
            new LoginDto { Email = "chef@example.com", Password = "Secret123!" });

        Assert.Equal("jwt-value", login.AccessToken);
        Assert.Equal("chef@example.com", login.Email);
    }

    [Fact]
    public async Task LoginAsync_without_slug_throws_when_multiple_tenants()
    {
        using var fx = new NoTenantDbFixture();
        await SeedFeaturesAsync(fx);
        var sut = CreateSut(fx);

        await sut.RegisterTenantAsync(
            new RegisterTenantDto
            {
                TenantName = "Cafe Norte",
                AdminEmail = "multi@example.com",
                Password = "Secret123!",
            });

        var tenantB = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Cafe Sur",
            Slug = "cafe-sur",
            IsActive = true,
        };
        await fx.Db.Tenants.AddAsync(tenantB);

        var user = await fx.Db.Users.FirstAsync(u => u.Email == "multi@example.com");
        var role = new Role
        {
            Id = Guid.NewGuid(),
            TenantId = tenantB.Id,
            Name = SystemRoles.Manager,
            NormalizedName = SystemRoles.Manager.ToUpperInvariant(),
        };
        await fx.Db.Roles.AddAsync(role);
        var tenantUser = new TenantUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantB.Id,
            UserId = user.Id,
            IsActive = true,
        };
        tenantUser.TenantUserRoles.Add(new TenantUserRole
        {
            Id = Guid.NewGuid(),
            TenantId = tenantB.Id,
            TenantUserId = tenantUser.Id,
            RoleId = role.Id,
        });
        await fx.Db.TenantUsers.AddAsync(tenantUser);
        await fx.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<MultipleTenantsLoginException>(() =>
            sut.LoginAsync(new LoginDto { Email = "multi@example.com", Password = "Secret123!" }));

        Assert.Equal(2, ex.Tenants.Count);
        Assert.Contains(ex.Tenants, t => t.Slug == "cafe-norte");
        Assert.Contains(ex.Tenants, t => t.Slug == "cafe-sur");
    }
}
