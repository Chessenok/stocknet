using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockNet.Api.Contracts.Stocks;
using StockNet.Api.Domain.Entities;
using StockNet.Api.Infrastructure.Authorization;
using StockNet.Api.Infrastructure.Persistence;
using StockNet.Api.Services;

namespace StockNet.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/stocks")]
public sealed class StocksController(
    StockNetDbContext dbContext,
    StockSubscriptionGuard subscriptionGuard) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<StockDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockDto>> Create(
        [FromBody] CreateStockRequest request,
        CancellationToken cancellationToken)
    {
        var companyId = User.GetCompanyId();
        if (!await subscriptionGuard.CanPostStockAsync(companyId, cancellationToken))
        {
            return SubscriptionRequired();
        }

        var warehouseId = request.WarehouseId!.Value;
        var ownsWarehouse = await dbContext.Warehouses
            .AsNoTracking()
            .AnyAsync(
                warehouse => warehouse.Id == warehouseId && warehouse.CompanyId == companyId,
                cancellationToken);

        if (!ownsWarehouse)
        {
            return NotFound();
        }

        if (!await IsActiveCategoryAsync(request.CategoryId, cancellationToken))
        {
            return InvalidCategory();
        }

        using var attributes = ParseAttributes(request.Attributes);
        var now = DateTime.UtcNow;
        var stock = new Stock
        {
            WarehouseId = warehouseId,
            CategoryId = request.CategoryId,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            TotalQuantityTons = request.TotalQuantityTons!.Value,
            MinOrderQuantityTons = request.MinOrderQuantityTons!.Value,
            StepQuantityTons = request.StepQuantityTons!.Value,
            PackagingType = request.PackagingType.Trim(),
            PricePerTon = request.PricePerTon!.Value,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            Attributes = attributes,
            AvailableFrom = request.AvailableFrom!.Value.UtcDateTime,
            AvailableTo = request.AvailableTo!.Value.UtcDateTime,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Stocks.Add(stock);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await GetOwnedStockDtoAsync(stock.Id, companyId, cancellationToken);
        return Created("/api/stocks/my", response);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<StockDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockDto>> UpdateContent(
        Guid id,
        [FromBody] UpdateStockRequest request,
        CancellationToken cancellationToken)
    {
        var companyId = User.GetCompanyId();
        var stock = await dbContext.Stocks
            .SingleOrDefaultAsync(
                item => item.Id == id && item.Warehouse.CompanyId == companyId,
                cancellationToken);

        if (stock is null)
        {
            return NotFound();
        }

        if (!await subscriptionGuard.CanPostStockAsync(companyId, cancellationToken))
        {
            return SubscriptionRequired();
        }

        if (!await IsActiveCategoryAsync(request.CategoryId, cancellationToken))
        {
            return InvalidCategory();
        }

        using var previousAttributes = stock.Attributes;
        using var attributes = ParseAttributes(request.Attributes);
        ApplyContent(stock, request, attributes);
        stock.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await GetOwnedStockDtoAsync(id, companyId, cancellationToken);
        return Ok(response);
    }

    [HttpPatch("{id:guid}/archive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var companyId = User.GetCompanyId();
        var stock = await dbContext.Stocks
            .SingleOrDefaultAsync(
                item => item.Id == id && item.Warehouse.CompanyId == companyId,
                cancellationToken);

        if (stock is null)
        {
            return NotFound();
        }

        stock.IsActive = false;
        stock.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpGet("my")]
    [ProducesResponseType<IReadOnlyList<StockDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StockDto>>> ListMine(CancellationToken cancellationToken)
    {
        var companyId = User.GetCompanyId();
        var stockRows = await dbContext.Stocks
            .AsNoTracking()
            .Where(stock => stock.Warehouse.CompanyId == companyId)
            .OrderByDescending(stock => stock.CreatedAt)
            .ThenBy(stock => stock.Id)
            .Select(StockProjection.Projection)
            .ToListAsync(cancellationToken);

        return Ok(stockRows.Select(stock => stock.ToDto()).ToList());
    }

    private Task<bool> IsActiveCategoryAsync(int categoryId, CancellationToken cancellationToken) =>
        dbContext.Categories
            .AsNoTracking()
            .AnyAsync(category => category.Id == categoryId && category.IsActive, cancellationToken);

    private async Task<StockDto?> GetOwnedStockDtoAsync(
        Guid id,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var row = await dbContext.Stocks
            .AsNoTracking()
            .Where(stock => stock.Id == id && stock.Warehouse.CompanyId == companyId)
            .Select(StockProjection.Projection)
            .SingleOrDefaultAsync(cancellationToken);

        return row?.ToDto();
    }

    private static void ApplyContent(Stock stock, StockContentRequest request, JsonDocument? attributes)
    {
        stock.CategoryId = request.CategoryId;
        stock.Title = request.Title.Trim();
        stock.Description = request.Description.Trim();
        stock.TotalQuantityTons = request.TotalQuantityTons!.Value;
        stock.MinOrderQuantityTons = request.MinOrderQuantityTons!.Value;
        stock.StepQuantityTons = request.StepQuantityTons!.Value;
        stock.PackagingType = request.PackagingType.Trim();
        stock.PricePerTon = request.PricePerTon!.Value;
        stock.Currency = request.Currency.Trim().ToUpperInvariant();
        stock.Attributes = attributes;
        stock.AvailableFrom = request.AvailableFrom!.Value.UtcDateTime;
        stock.AvailableTo = request.AvailableTo!.Value.UtcDateTime;
    }

    private static JsonDocument? ParseAttributes(JsonElement? attributes) =>
        attributes is { } value ? JsonDocument.Parse(value.GetRawText()) : null;

    private ObjectResult SubscriptionRequired() => StatusCode(
        StatusCodes.Status403Forbidden,
        new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "An active subscription is required to create or update stock content."
        });

    private BadRequestObjectResult InvalidCategory() => BadRequest(new ProblemDetails
    {
        Status = StatusCodes.Status400BadRequest,
        Title = "The selected category does not exist or is inactive."
    });
}
