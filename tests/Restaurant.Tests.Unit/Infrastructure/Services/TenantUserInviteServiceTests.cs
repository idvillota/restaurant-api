using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Features.Organization.TenantUsers;
using Restaurant.Domain.Entities;
using Restaurant.Infrastructure.Authorization;
using Restaurant.Infrastructure.Identity;
using Restaurant.Infrastructure.Services;
using Restaurant.Tests.Unit.Support;

namespace Restaurant.Tests.Unit.Infrastructure.Services;

public sealed class TenantUserInviteServiceTests
{
    private static async Task SeedTenantWithRolesAsync(TenantDbFixture fx)
    {
        await fx.Db.Tenants.AddAsync(new Tenant
        {
            Id = fx.TenantId,
            Name = "Test Cafe",
            Slug = "test-cafe",
            IsActive = true,
        });

        foreach (var name in new[] { SystemRoles.Owner, SystemRoles.Manager, SystemRoles.Staff })
        {
            await fx.Db.Roles.AddAsync(new Role
            {
                Id = Guid.NewGuid(),
                TenantId = fx.TenantId,
                Name = name,
                NormalizedName = name.ToUpperInvariant(),
            });
        }

        await fx.Db.SaveChangesAsync();
    }

    private static TenantUserInviteService CreateSut(TenantDbFixture fx) =>
        new(fx.UnitOfWork, fx.TenantContext, new BcryptPasswordHasher());

    [Fact]
    public async Task InviteAsync_creates_user_membership_and_staff_role()
    {
        using var fx = new TenantDbFixture();
        await SeedTenantWithRolesAsync(fx);
        var sut = CreateSut(fx);

        var result = await sut.InviteAsync(
            new InviteTenantUserDto
            {
                Email = "newchef@example.com",
                Password = "Str0ng!Pass",
                Role = SystemRoles.Staff,
            });

        Assert.Equal("newchef@example.com", result.Email);
        Assert.Single(result.Roles);
        Assert.Equal(SystemRoles.Staff, result.Roles[0]);

        var user = await fx.Db.Users.AsNoTracking().SingleAsync(u => u.Email == "newchef@example.com");
        Assert.Equal(result.UserId, user.Id);

        var tu = await fx.Db.TenantUsers.AsNoTracking().SingleAsync(t => t.Id == result.TenantUserId);
        Assert.Equal(fx.TenantId, tu.TenantId);
        Assert.Equal(user.Id, tu.UserId);

        var tur = await fx.Db.TenantUserRoles.AsNoTracking().SingleAsync();
        Assert.Equal(tu.Id, tur.TenantUserId);
    }

    [Fact]
    public async Task InviteAsync_adds_membership_when_user_exists_in_another_tenant()
    {
        using var fx = new TenantDbFixture();
        await SeedTenantWithRolesAsync(fx);

        var otherTenantId = Guid.NewGuid();
        // Clear tenant so SaveChanges does not stamp ITenantScoped rows with the fixture tenant id.
        fx.TenantContext.TenantId = null;

        await fx.Db.Tenants.AddAsync(new Tenant
        {
            Id = otherTenantId,
            Name = "Other",
            Slug = "other",
            IsActive = true,
        });
        await fx.Db.Roles.AddRangeAsync(
            new Role { Id = Guid.NewGuid(), TenantId = otherTenantId, Name = SystemRoles.Owner, NormalizedName = "OWNER" },
            new Role { Id = Guid.NewGuid(), TenantId = otherTenantId, Name = SystemRoles.Manager, NormalizedName = "MANAGER" },
            new Role { Id = Guid.NewGuid(), TenantId = otherTenantId, Name = SystemRoles.Staff, NormalizedName = "STAFF" });
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "shared@example.com",
            NormalizedEmail = "SHARED@EXAMPLE.COM",
            PasswordHash = new BcryptPasswordHasher().Hash("Existing1!"),
        };
        await fx.Db.Users.AddAsync(existingUser);
        await fx.Db.TenantUsers.AddAsync(new TenantUser
        {
            Id = Guid.NewGuid(),
            TenantId = otherTenantId,
            UserId = existingUser.Id,
        });
        await fx.Db.SaveChangesAsync();

        fx.TenantContext.TenantId = fx.TenantId;

        var sut = CreateSut(fx);
        var result = await sut.InviteAsync(
            new InviteTenantUserDto
            {
                Email = "shared@example.com",
                Password = null,
                Role = SystemRoles.Manager,
            });

        Assert.Equal(existingUser.Id, result.UserId);
        Assert.Equal(SystemRoles.Manager, result.Roles[0]);

        var memberships = await fx.Db.TenantUsers.AsNoTracking()
            .Where(t => t.UserId == existingUser.Id && t.TenantId == fx.TenantId)
            .ToListAsync();
        Assert.Single(memberships);
    }

    [Fact]
    public async Task InviteAsync_throws_when_user_already_in_tenant()
    {
        using var fx = new TenantDbFixture();
        await SeedTenantWithRolesAsync(fx);

        var sut = CreateSut(fx);
        await sut.InviteAsync(
            new InviteTenantUserDto
            {
                Email = "dup@example.com",
                Password = "Str0ng!Pass",
                Role = SystemRoles.Staff,
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.InviteAsync(
                new InviteTenantUserDto
                {
                    Email = "dup@example.com",
                    Password = "Another1!Pass",
                    Role = SystemRoles.Manager,
                }));
    }

    [Fact]
    public async Task InviteAsync_throws_when_password_missing_for_new_user()
    {
        using var fx = new TenantDbFixture();
        await SeedTenantWithRolesAsync(fx);
        var sut = CreateSut(fx);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.InviteAsync(
                new InviteTenantUserDto
                {
                    Email = "nopwd@example.com",
                    Password = null,
                    Role = SystemRoles.Staff,
                }));
    }

    [Fact]
    public async Task InviteAsync_throws_when_tenant_inactive()
    {
        using var fx = new TenantDbFixture();
        await fx.Db.Tenants.AddAsync(new Tenant
        {
            Id = fx.TenantId,
            Name = "Inactive",
            Slug = "inactive",
            IsActive = false,
        });
        foreach (var name in new[] { SystemRoles.Owner, SystemRoles.Manager, SystemRoles.Staff })
        {
            await fx.Db.Roles.AddAsync(new Role
            {
                Id = Guid.NewGuid(),
                TenantId = fx.TenantId,
                Name = name,
                NormalizedName = name.ToUpperInvariant(),
            });
        }

        await fx.Db.SaveChangesAsync();
        var sut = CreateSut(fx);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.InviteAsync(
                new InviteTenantUserDto
                {
                    Email = "x@example.com",
                    Password = "Str0ng!Pass",
                    Role = SystemRoles.Staff,
                }));
    }
}
