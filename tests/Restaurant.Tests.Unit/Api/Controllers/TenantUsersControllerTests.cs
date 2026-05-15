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
}
