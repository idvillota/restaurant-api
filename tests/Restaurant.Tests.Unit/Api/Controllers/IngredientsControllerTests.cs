using Microsoft.AspNetCore.Mvc;
using Moq;
using Restaurant.Api.Controllers;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Catalog.Ingredients;
using Restaurant.Domain.Enums;

namespace Restaurant.Tests.Unit.Api.Controllers;

public sealed class IngredientsControllerTests
{
    private readonly Mock<IIngredientService> _service = new();
    private readonly IngredientsController _controller;

    public IngredientsControllerTests() => _controller = new IngredientsController(_service.Object);

    [Fact]
    public async Task GetById_returns_not_found_when_missing()
    {
        var id = Guid.NewGuid();
        _service.Setup(s => s.GetByIdAsync(id, default)).ReturnsAsync((IngredientDto?)null);

        var result = await _controller.GetById(id);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetById_returns_ok_with_dto()
    {
        var id = Guid.NewGuid();
        var cat = Guid.NewGuid();
        var dto = new IngredientDto
        {
            Id = id,
            IngredientCategoryId = cat,
            IngredientCategoryName = "General",
            Name = "Tomato",
            Unit = IngredientUnit.Unit,
            IsActive = true,
        };
        _service.Setup(s => s.GetByIdAsync(id, default)).ReturnsAsync(dto);

        var result = await _controller.GetById(id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<IngredientDto>(ok.Value);
        Assert.Equal("Tomato", body.Name);
    }

    [Fact]
    public async Task Create_returns_created_at_route()
    {
        var cat = Guid.NewGuid();
        var created = new IngredientDto
        {
            Id = Guid.NewGuid(),
            IngredientCategoryId = cat,
            IngredientCategoryName = "Herbs",
            Name = "Basil",
            Unit = IngredientUnit.Gram,
            IsActive = true,
        };
        _service.Setup(s => s.CreateAsync(It.IsAny<CreateIngredientDto>(), default)).ReturnsAsync(created);

        var result = await _controller.Create(
            new CreateIngredientDto
            {
                IngredientCategoryId = cat,
                Name = "Basil",
                Unit = IngredientUnit.Gram,
            });

        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(IngredientsController.GetById), createdResult.ActionName);
        Assert.Equal(created.Id, ((IngredientDto)createdResult.Value!).Id);
    }

    [Fact]
    public async Task Create_returns_conflict_on_duplicate()
    {
        _service.Setup(s => s.CreateAsync(It.IsAny<CreateIngredientDto>(), default))
            .ThrowsAsync(new InvalidOperationException("duplicate"));

        var result = await _controller.Create(
            new CreateIngredientDto
            {
                IngredientCategoryId = Guid.NewGuid(),
                Name = "X",
                Unit = IngredientUnit.Unit,
            });

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task SoftDelete_returns_no_content_when_deleted()
    {
        var id = Guid.NewGuid();
        _service.Setup(s => s.SoftDeleteAsync(id, default)).ReturnsAsync(true);

        var result = await _controller.SoftDelete(id);

        Assert.IsType<NoContentResult>(result);
    }
}
