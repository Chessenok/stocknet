using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockNet.Api.Contracts.Categories;
using StockNet.Api.Domain.Entities;
using StockNet.Api.Infrastructure.Persistence;

namespace StockNet.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/categories")]
public sealed class CategoriesController(StockNetDbContext dbContext) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CategoryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> List(CancellationToken cancellationToken)
    {
        var categories = await dbContext.Categories
            .AsNoTracking()
            .Where(category => category.IsActive)
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Id)
            .ToListAsync(cancellationToken);

        return Ok(categories.Select(ToDto).ToArray());
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<CategoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryDto>> Get(int id, CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id && item.IsActive, cancellationToken);

        return category is null ? NotFound() : Ok(ToDto(category));
    }

    private static CategoryDto ToDto(Category category) => new(
        category.Id,
        category.ParentId,
        category.Name,
        category.Slug,
        category.Description,
        category.SortOrder,
        category.AttributeRules.Select(rule => new CategoryAttributeRuleDto(
            rule.Key,
            rule.Type,
            rule.IsRequired,
            rule.Min,
            rule.Max,
            rule.AllowedValues.ToArray())).ToArray());
}
