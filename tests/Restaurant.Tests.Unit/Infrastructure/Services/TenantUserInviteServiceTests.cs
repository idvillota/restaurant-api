using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Features.Organization.TenantUsers;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
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

        foreach (var name in new[] { SystemRoles.Administrator, SystemRoles.Manager, SystemRoles.Waitress, SystemRoles.Cashier })
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
        Assert.Equal(SystemRoles.Waitress, result.Roles[0]);

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
        foreach (var name in new[] { SystemRoles.Administrator, SystemRoles.Manager, SystemRoles.Waitress, SystemRoles.Cashier })
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

    [Fact]
    public async Task ChangeRoleAsync_replaces_all_roles_with_a_single_role()
    {
        using var fx = new TenantDbFixture();
        await SeedTenantWithRolesAsync(fx);
        var actor = await SeedMemberAsync(fx, "admin@example.com", SystemRoles.Administrator);
        var target = await SeedMemberAsync(fx, "john@example.com", SystemRoles.Waitress, "John Doe");
        var extraRole = await fx.Db.Roles.SingleAsync(r => r.Name == SystemRoles.Cashier);
        fx.Db.TenantUserRoles.Add(new TenantUserRole
        {
            Id = Guid.NewGuid(),
            TenantId = fx.TenantId,
            TenantUserId = target.Membership.Id,
            RoleId = extraRole.Id,
        });
        await fx.Db.SaveChangesAsync();
        fx.TenantContext.UserId = actor.User.Id;

        var sut = CreateSut(fx);
        var result = await sut.ChangeRoleAsync(
            target.Membership.Id,
            new ChangeTenantUserRoleDto { Role = SystemRoles.Manager });

        Assert.Equal(SystemRoles.Manager, result.Role);
        Assert.Equal("john@example.com", result.Email);
        var remaining = await fx.Db.TenantUserRoles.AsNoTracking()
            .Where(tur => tur.TenantUserId == target.Membership.Id)
            .ToListAsync();
        Assert.Single(remaining);
        var manager = await fx.Db.Roles.SingleAsync(r => r.Name == SystemRoles.Manager);
        Assert.Equal(manager.Id, remaining[0].RoleId);
    }

    [Fact]
    public async Task ChangeRoleAsync_throws_when_changing_own_role()
    {
        using var fx = new TenantDbFixture();
        await SeedTenantWithRolesAsync(fx);
        var actor = await SeedMemberAsync(fx, "self@example.com", SystemRoles.Administrator);
        fx.TenantContext.UserId = actor.User.Id;
        var sut = CreateSut(fx);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ChangeRoleAsync(actor.Membership.Id, new ChangeTenantUserRoleDto { Role = SystemRoles.Manager }));
        Assert.Contains("own role", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChangeRoleAsync_throws_when_assigning_administrator()
    {
        using var fx = new TenantDbFixture();
        await SeedTenantWithRolesAsync(fx);
        var actor = await SeedMemberAsync(fx, "admin@example.com", SystemRoles.Administrator);
        var target = await SeedMemberAsync(fx, "john@example.com", SystemRoles.Waitress);
        fx.TenantContext.UserId = actor.User.Id;
        var sut = CreateSut(fx);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ChangeRoleAsync(
                target.Membership.Id,
                new ChangeTenantUserRoleDto { Role = SystemRoles.Administrator }));
    }

    [Fact]
    public async Task ChangeRoleAsync_throws_when_manager_changes_administrator()
    {
        using var fx = new TenantDbFixture();
        await SeedTenantWithRolesAsync(fx);
        var lastAdmin = await SeedMemberAsync(fx, "admin@example.com", SystemRoles.Administrator);
        var manager = await SeedMemberAsync(fx, "mgr@example.com", SystemRoles.Manager);
        fx.TenantContext.UserId = manager.User.Id;
        var sut = CreateSut(fx);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ChangeRoleAsync(lastAdmin.Membership.Id, new ChangeTenantUserRoleDto { Role = SystemRoles.Manager }));
        Assert.Contains("Only an Administrator", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChangeRoleAsync_allows_demoting_administrator_when_another_remains()
    {
        using var fx = new TenantDbFixture();
        await SeedTenantWithRolesAsync(fx);
        var actor = await SeedMemberAsync(fx, "admin1@example.com", SystemRoles.Administrator);
        var target = await SeedMemberAsync(fx, "admin2@example.com", SystemRoles.Administrator);
        fx.TenantContext.UserId = actor.User.Id;
        var sut = CreateSut(fx);

        var result = await sut.ChangeRoleAsync(
            target.Membership.Id,
            new ChangeTenantUserRoleDto { Role = SystemRoles.Manager });

        Assert.Equal(SystemRoles.Manager, result.Role);
    }

    [Fact]
    public async Task ListAsync_returns_a_single_primary_role()
    {
        using var fx = new TenantDbFixture();
        await SeedTenantWithRolesAsync(fx);
        var member = await SeedMemberAsync(fx, "john@example.com", SystemRoles.Waitress, "John Doe");
        var extraRole = await fx.Db.Roles.SingleAsync(r => r.Name == SystemRoles.Cashier);
        fx.Db.TenantUserRoles.Add(new TenantUserRole
        {
            Id = Guid.NewGuid(),
            TenantId = fx.TenantId,
            TenantUserId = member.Membership.Id,
            RoleId = extraRole.Id,
        });
        await fx.Db.SaveChangesAsync();

        var sut = CreateSut(fx);
        var list = await sut.ListAsync(false);

        var row = Assert.Single(list);
        Assert.Equal(SystemRoles.Cashier, row.Role);
        Assert.Equal("John Doe", row.DisplayName);
    }

    [Fact]
    public async Task SetActiveAsync_disables_membership()
    {
        using var fx = new TenantDbFixture();
        await SeedTenantWithRolesAsync(fx);
        var actor = await SeedMemberAsync(fx, "admin@example.com", SystemRoles.Administrator);
        var target = await SeedMemberAsync(fx, "john@example.com", SystemRoles.Waitress);
        fx.TenantContext.UserId = actor.User.Id;
        var sut = CreateSut(fx);

        var result = await sut.SetActiveAsync(target.Membership.Id, false);

        Assert.False(result.IsActive);
        var stored = await fx.Db.TenantUsers.AsNoTracking().SingleAsync(tu => tu.Id == target.Membership.Id);
        Assert.False(stored.IsActive);
        Assert.DoesNotContain(await sut.ListAsync(false), m => m.Email == "john@example.com");
        Assert.Contains(await sut.ListAsync(true), m => m.Email == "john@example.com" && !m.IsActive);
    }

    [Fact]
    public async Task SetActiveAsync_throws_when_changing_own_access()
    {
        using var fx = new TenantDbFixture();
        await SeedTenantWithRolesAsync(fx);
        var actor = await SeedMemberAsync(fx, "admin@example.com", SystemRoles.Administrator);
        fx.TenantContext.UserId = actor.User.Id;
        var sut = CreateSut(fx);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.SetActiveAsync(actor.Membership.Id, false));
    }

    [Fact]
    public async Task SetActiveAsync_throws_when_open_cashier_shift()
    {
        using var fx = new TenantDbFixture();
        await SeedTenantWithRolesAsync(fx);
        var actor = await SeedMemberAsync(fx, "admin@example.com", SystemRoles.Administrator);
        var target = await SeedMemberAsync(fx, "cajero@example.com", SystemRoles.Cashier);
        fx.Db.CashierShifts.Add(new CashierShift
        {
            Id = Guid.NewGuid(),
            TenantId = fx.TenantId,
            CashierUserId = target.User.Id,
            Status = CashierShiftStatus.Open,
            BusinessDate = new DateOnly(2026, 11, 9),
            OpenedAtUtc = DateTime.UtcNow,
            OpeningFloat = 0,
        });
        await fx.Db.SaveChangesAsync();
        fx.TenantContext.UserId = actor.User.Id;
        var sut = CreateSut(fx);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.SetActiveAsync(target.Membership.Id, false));
        Assert.Contains("cashier shift", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InviteAsync_reactivates_inactive_membership()
    {
        using var fx = new TenantDbFixture();
        await SeedTenantWithRolesAsync(fx);
        var member = await SeedMemberAsync(fx, "john@example.com", SystemRoles.Waitress);
        member.Membership.IsActive = false;
        await fx.Db.SaveChangesAsync();
        var sut = CreateSut(fx);

        var result = await sut.InviteAsync(
            new InviteTenantUserDto
            {
                Email = "john@example.com",
                Role = SystemRoles.Cashier,
            });

        Assert.Equal(member.Membership.Id, result.TenantUserId);
        Assert.Equal(SystemRoles.Cashier, result.Roles[0]);
        var stored = await fx.Db.TenantUsers.AsNoTracking().SingleAsync(tu => tu.Id == member.Membership.Id);
        Assert.True(stored.IsActive);
        var roles = await fx.Db.TenantUserRoles.AsNoTracking()
            .Where(tur => tur.TenantUserId == member.Membership.Id)
            .ToListAsync();
        Assert.Single(roles);
    }

    private static async Task<(User User, TenantUser Membership)> SeedMemberAsync(
        TenantDbFixture fx,
        string email,
        string roleName,
        string? displayName = null)
    {
        var role = await fx.Db.Roles.SingleAsync(r => r.Name == roleName);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            PasswordHash = "hash",
            DisplayName = displayName,
        };
        var membership = new TenantUser
        {
            Id = Guid.NewGuid(),
            TenantId = fx.TenantId,
            UserId = user.Id,
        };
        membership.TenantUserRoles.Add(new TenantUserRole
        {
            Id = Guid.NewGuid(),
            TenantId = fx.TenantId,
            TenantUserId = membership.Id,
            RoleId = role.Id,
        });
        await fx.Db.Users.AddAsync(user);
        await fx.Db.TenantUsers.AddAsync(membership);
        await fx.Db.SaveChangesAsync();
        return (user, membership);
    }
}
