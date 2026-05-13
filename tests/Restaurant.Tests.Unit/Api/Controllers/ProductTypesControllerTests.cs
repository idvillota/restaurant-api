using Microsoft.AspNetCore.Mvc;
using Moq;
using Restaurant.Api.Controllers;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Catalog.ProductTypes;

namespace Restaurant.Tests.Unit.Api.Controllers;

public sealed class ProductTypesControllerTests
{
    [Fact]
    public async Task GetById_returns_ok()
    {
        var id = Guid.NewGuid();
        var mock = new Mock<IProductTypeService>();
        mock.Setup(s => s.GetByIdAsync(id, default)).ReturnsAsync(new ProductTypeDto { Id = id, Name = "Food", SortOrder = 0, IsActive = true });
        var controller = new ProductTypesController(mock.Object);

        var result = await controller.GetById(id);

        Assert.IsType<OkObjectResult>(result.Result);
    }
}
