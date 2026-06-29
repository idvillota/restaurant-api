using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Restaurant.Api.Controllers;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.PublicMenu;

namespace Restaurant.Tests.Unit.Api.Controllers;

public sealed class PublicMenuControllerTests
{
    [Fact]
    public void GetByTenantSlug_allows_anonymous_access()
    {
        var method = typeof(PublicMenuController).GetMethod(nameof(PublicMenuController.GetByTenantSlug));
        Assert.NotNull(method);

        var allowsAnonymous =
            method.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any()
            || typeof(PublicMenuController).GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any();

        Assert.True(allowsAnonymous);
    }

    [Fact]
    public async Task GetByTenantSlug_returns_ok_when_menu_exists()
    {
        var menu = new PublicMenuDto
        {
            TenantName = "Bistró Demo",
            CurrencyCode = "COP",
            Categories = [],
        };

        var mock = new Mock<IPublicMenuService>();
        mock.Setup(s => s.GetByTenantSlugAsync("demo-bistro", It.IsAny<CancellationToken>())).ReturnsAsync(menu);

        var controller = new PublicMenuController(mock.Object);
        var result = await controller.GetByTenantSlug("demo-bistro", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(menu, ok.Value);
    }

    [Fact]
    public async Task GetByTenantSlug_returns_not_found_when_menu_missing()
    {
        var mock = new Mock<IPublicMenuService>();
        mock.Setup(s => s.GetByTenantSlugAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PublicMenuDto?)null);

        var controller = new PublicMenuController(mock.Object);
        var result = await controller.GetByTenantSlug("missing", CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
