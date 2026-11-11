using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Restaurant.Api.Controllers;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Organization.TenantUsers;

namespace Restaurant.Tests.Unit.Api.Controllers;

public sealed class TenantUsersControllerTests
{
    private readonly Mock<ITenantUserInviteService> _service = new();
    private readonly TenantUsersController _controller;

    public TenantUsersControllerTests() => _controller = new TenantUsersController(_service.Object);

    [Fact]
    public async Task Invite_returns_201_when_successful()
    {
        var dto = new InvitedTenantUserDto
        {
            UserId = Guid.NewGuid(),
            TenantUserId = Guid.NewGuid(),
            Email = "chef@example.com",
            Roles = new[] { "Staff" },
        };
        _service.Setup(s => s.InviteAsync(It.IsAny<InviteTenantUserDto>(), default)).ReturnsAsync(dto);

        var result = await _controller.Invite(new InviteTenantUserDto
        {
            Email = "chef@example.com",
            Password = "Str0ng!Pass",
            Role = "Staff",
        });

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, objectResult.StatusCode);
        var body = Assert.IsType<InvitedTenantUserDto>(objectResult.Value);
        Assert.Equal("chef@example.com", body.Email);
    }

    [Fact]
    public async Task Invite_returns_conflict_when_already_member()
    {
        _service.Setup(s => s.InviteAsync(It.IsAny<InviteTenantUserDto>(), default))
            .ThrowsAsync(new InvalidOperationException("This user is already a member of this tenant."));

        var result = await _controller.Invite(new InviteTenantUserDto
        {
            Email = "dup@example.com",
            Password = "Str0ng!Pass",
            Role = "Staff",
        });

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task Invite_returns_bad_request_on_validation_error_from_service()
    {
        _service.Setup(s => s.InviteAsync(It.IsAny<InviteTenantUserDto>(), default))
            .ThrowsAsync(new InvalidOperationException("Password is required for new users."));

        var result = await _controller.Invite(new InviteTenantUserDto
        {
            Email = "new@example.com",
            Role = "Staff",
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task List_returns_ok()
    {
        var members = new List<TenantUserMemberDto>
        {
            new()
            {
                UserId = Guid.NewGuid(),
                TenantUserId = Guid.NewGuid(),
                Email = "john@example.com",
                Role = "Waitress",
                IsActive = true,
            },
        };
        _service.Setup(s => s.ListAsync(false, default)).ReturnsAsync(members);

        var result = await _controller.List();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsAssignableFrom<IReadOnlyList<TenantUserMemberDto>>(ok.Value);
        Assert.Equal("john@example.com", body[0].Email);
    }

    [Fact]
    public async Task ChangeRole_returns_ok()
    {
        var tenantUserId = Guid.NewGuid();
        var dto = new TenantUserMemberDto
        {
            UserId = Guid.NewGuid(),
            TenantUserId = tenantUserId,
            Email = "john@example.com",
            Role = "Manager",
            IsActive = true,
        };
        _service.Setup(s => s.ChangeRoleAsync(tenantUserId, It.IsAny<ChangeTenantUserRoleDto>(), default))
            .ReturnsAsync(dto);

        var result = await _controller.ChangeRole(tenantUserId, new ChangeTenantUserRoleDto { Role = "Manager" });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<TenantUserMemberDto>(ok.Value);
        Assert.Equal("Manager", body.Role);
    }

    [Fact]
    public async Task ChangeRole_returns_bad_request_on_service_error()
    {
        _service.Setup(s => s.ChangeRoleAsync(It.IsAny<Guid>(), It.IsAny<ChangeTenantUserRoleDto>(), default))
            .ThrowsAsync(new InvalidOperationException("You cannot change your own role."));

        var result = await _controller.ChangeRole(Guid.NewGuid(), new ChangeTenantUserRoleDto { Role = "Manager" });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task SetActive_returns_ok()
    {
        var tenantUserId = Guid.NewGuid();
        var dto = new TenantUserMemberDto
        {
            UserId = Guid.NewGuid(),
            TenantUserId = tenantUserId,
            Email = "john@example.com",
            Role = "Waitress",
            IsActive = false,
        };
        _service.Setup(s => s.SetActiveAsync(tenantUserId, false, default)).ReturnsAsync(dto);

        var result = await _controller.SetActive(tenantUserId, new SetTenantUserActiveDto { IsActive = false });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<TenantUserMemberDto>(ok.Value);
        Assert.False(body.IsActive);
    }

    [Fact]
    public async Task UpdateProfile_returns_ok()
    {
        var tenantUserId = Guid.NewGuid();
        var dto = new TenantUserMemberDto
        {
            UserId = Guid.NewGuid(),
            TenantUserId = tenantUserId,
            Email = "new@example.com",
            DisplayName = "Juan",
            Role = "Waitress",
            IsActive = true,
        };
        _service.Setup(s => s.UpdateProfileAsync(tenantUserId, It.IsAny<UpdateTenantUserProfileDto>(), default))
            .ReturnsAsync(dto);

        var result = await _controller.UpdateProfile(
            tenantUserId,
            new UpdateTenantUserProfileDto { Email = "new@example.com", DisplayName = "Juan" });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<TenantUserMemberDto>(ok.Value);
        Assert.Equal("new@example.com", body.Email);
    }

    [Fact]
    public async Task UpdateProfile_returns_conflict_when_email_taken()
    {
        _service.Setup(s => s.UpdateProfileAsync(It.IsAny<Guid>(), It.IsAny<UpdateTenantUserProfileDto>(), default))
            .ThrowsAsync(new InvalidOperationException("Email is already registered."));

        var result = await _controller.UpdateProfile(
            Guid.NewGuid(),
            new UpdateTenantUserProfileDto { Email = "taken@example.com" });

        Assert.IsType<ConflictObjectResult>(result.Result);
    }
}
