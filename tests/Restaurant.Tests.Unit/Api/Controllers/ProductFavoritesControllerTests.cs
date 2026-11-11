using Microsoft.AspNetCore.Mvc;
using Moq;
using Restaurant.Api.Controllers;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.ProductFavorites;

namespace Restaurant.Tests.Unit.Api.Controllers;

public sealed class ProductFavoritesControllerTests
{
    private readonly Mock<IProductFavoriteService> _service = new();
    private readonly ProductFavoritesController _controller;

    public ProductFavoritesControllerTests() => _controller = new ProductFavoritesController(_service.Object);

    [Fact]
    public async Task Add_returns_bad_request_when_at_max()
    {
        var productId = Guid.NewGuid();
        _service.Setup(s => s.AddAsync(productId, default))
            .ThrowsAsync(new InvalidOperationException("Maximum of 10 favorite products."));

        var result = await _controller.Add(productId);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task List_returns_ok()
    {
        var dto = new ProductFavoriteListDto { ProductIds = new[] { Guid.NewGuid() }, MaxCount = 10 };
        _service.Setup(s => s.ListAsync(default)).ReturnsAsync(dto);

        var result = await _controller.List();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(dto, ok.Value);
    }
}
