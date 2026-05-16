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
        var mock = new Mock<IProductService>();
        var items = new List<ProductListItemDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Item",
                Description = "Test description",
                Sku = null,
                UnitPrice = 1m,
                CostPrice = 0m,
                ProductTypeId = Guid.NewGuid(),
                ProductTypeName = "Type",
                IsActive = true,
            },
        };
        mock.Setup(s => s.ListAsync(false, It.IsAny<CancellationToken>())).ReturnsAsync(items);
        var controller = new ProductsController(mock.Object);

        var result = await controller.List(false, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsAssignableFrom<IReadOnlyList<ProductListItemDto>>(ok.Value);
        Assert.Single(body!);
    }
}
