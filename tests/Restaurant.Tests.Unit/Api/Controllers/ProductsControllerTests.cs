using Microsoft.AspNetCore.Mvc;
using Moq;
using Restaurant.Api.Controllers;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Catalog;

namespace Restaurant.Tests.Unit.Api.Controllers;

public sealed class ProductsControllerTests
{
    [Fact]
    public async Task List_returns_ok_with_items()
    {
        var mock = new Mock<IProductReadService>();
        var items = new List<ProductListItemDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Item",
                Sku = null,
                UnitPrice = 1m,
                ProductTypeId = Guid.NewGuid(),
                ProductTypeName = "Type",
            },
        };
        mock.Setup(s => s.ListAsync(default)).ReturnsAsync(items);
        var controller = new ProductsController(mock.Object);

        var result = await controller.List(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsAssignableFrom<IReadOnlyList<ProductListItemDto>>(ok.Value);
        Assert.Single(body!);
    }
}
