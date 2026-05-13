using Microsoft.EntityFrameworkCore;
using Moq;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Auth;
using Restaurant.Infrastructure.Identity;
using Restaurant.Infrastructure.Services;
using Restaurant.Tests.Unit.Support;

namespace Restaurant.Tests.Unit.Infrastructure.Services;

public sealed class AuthServiceTests
{
    [Fact]
    public async Task RegisterTenantAsync_persists_tenant_user_roles_and_membership()
    {
        using var fx = new NoTenantDbFixture();
        var jwt = new Mock<IJwtTokenService>();
        jwt.Setup(j => j.CreateAccessToken(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()))
            .Returns("test-token");
        var sut = new AuthService(fx.UnitOfWork, new BcryptPasswordHasher(), jwt.Object);

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
        Assert.Equal(3, await fx.Db.Roles.CountAsync());
        Assert.Single(await fx.Db.TenantUsers.ToListAsync());
    }

    [Fact]
    public async Task RegisterTenantAsync_throws_when_email_already_used()
    {
        using var fx = new NoTenantDbFixture();
        var jwt = new Mock<IJwtTokenService>();
        jwt.Setup(j => j.CreateAccessToken(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()))
            .Returns("t");
        var sut = new AuthService(fx.UnitOfWork, new BcryptPasswordHasher(), jwt.Object);
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
        var jwt = new Mock<IJwtTokenService>();
        jwt.Setup(j => j.CreateAccessToken(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()))
            .Returns("jwt-value");
        var sut = new AuthService(fx.UnitOfWork, new BcryptPasswordHasher(), jwt.Object);
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
}
