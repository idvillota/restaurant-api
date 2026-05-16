using Microsoft.AspNetCore.Mvc;
using Moq;
using Restaurant.Api.Controllers;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Common.Models;
using Restaurant.Application.Features.Catalog;

namespace Restaurant.Tests.Unit.Api.Controllers;

public sealed class ProductsControllerTests
{
    [Fact]
    public async Task List_returns_ok_with_items()
    {
        var mock = new Mock<IProductService>();
        var page = new PagedResult<ProductListItemDto>
        {
            Items =
            [
                new ProductListItemDto
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
            ],
            TotalCount = 1,
            Page = 1,
            PageSize = 25,
        };
        mock.Setup(s => s.ListAsync(It.IsAny<ListQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(page);
        var controller = new ProductsController(mock.Object);

        var result = await controller.List(new ListQuery(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsAssignableFrom<PagedResult<ProductListItemDto>>(ok.Value);
        Assert.Single(body!.Items);
    }
}
