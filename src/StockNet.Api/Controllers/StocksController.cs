using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
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
    [AllowAnonymous]
    [HttpGet("map")]
    [ProducesResponseType<IReadOnlyList<StockMapDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StockMapDto>>> Map(
        [FromQuery] StockMapQuery query,
        CancellationToken cancellationToken)
    {
        JsonDocument? attributeFilter = null;
        if (!string.IsNullOrWhiteSpace(query.AttributeFilters))
        {
            try
            {
                attributeFilter = JsonDocument.Parse(query.AttributeFilters);
            }
            catch (JsonException)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "AttributeFilters must be a valid JSON object."
                });
            }

            if (attributeFilter.RootElement.ValueKind != JsonValueKind.Object)
            {
                attributeFilter.Dispose();
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "AttributeFilters must be a valid JSON object."
                });
            }
        }

        using var attributeFilterLifetime = attributeFilter;
        var center = new Point(query.Longitude!.Value, query.Latitude!.Value) { SRID = 4326 };
        var radiusMeters = query.Radius!.Value;
        var now = DateTime.UtcNow;

        var stocks = dbContext.Stocks
            .AsNoTracking()
            .Where(stock => stock.IsActive
                && stock.AvailableFrom <= now
                && stock.AvailableTo >= now
                && stock.Category.IsActive
                && stock.Warehouse.Location.IsWithinDistance(center, radiusMeters));

        if (query.CategoryId is { } categoryId)
        {
            stocks = stocks.Where(stock => stock.CategoryId == categoryId);
        }

        if (query.MinPrice is { } minPrice)
        {
            stocks = stocks.Where(stock => stock.PricePerUnit >= minPrice);
        }

        if (query.MaxPrice is { } maxPrice)
        {
            stocks = stocks.Where(stock => stock.PricePerUnit <= maxPrice);
        }

        if (query.Unit is { } unit)
        {
            stocks = stocks.Where(stock => stock.Unit == unit);
        }

        if (attributeFilter is not null)
        {
            stocks = stocks.Where(stock => stock.Attributes != null
                && EF.Functions.JsonContains(stock.Attributes, attributeFilter));
        }

        var rows = await stocks
            .OrderBy(stock => stock.Warehouse.Location.Distance(center))
            .ThenBy(stock => stock.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(StockMapProjection.Projection(center))
            .ToListAsync(cancellationToken);

        var results = rows.Select(row => row.ToDto()).ToList();
        return Ok(results);
    }

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

        var category = await GetActiveCategoryAsync(request.CategoryId, cancellationToken);
        if (category is null)
        {
            return InvalidCategory();
        }

        var attributeErrors = StockAttributeValidator.Validate(category, request.Attributes);
        if (attributeErrors.Count > 0)
        {
            return InvalidAttributes(attributeErrors);
        }

        using var attributes = ParseAttributes(request.Attributes);
        var now = DateTime.UtcNow;
        var stock = new Stock
        {
            WarehouseId = warehouseId,
            CategoryId = request.CategoryId,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            TotalQuantity = request.TotalQuantity!.Value,
            MinOrderQuantity = request.MinOrderQuantity!.Value,
            StepQuantity = request.StepQuantity!.Value,
            Unit = request.Unit,
            PackagingType = request.PackagingType.Trim(),
            PricePerUnit = request.PricePerUnit!.Value,
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

        var category = await GetActiveCategoryAsync(request.CategoryId, cancellationToken);
        if (category is null)
        {
            return InvalidCategory();
        }

        var attributeErrors = StockAttributeValidator.Validate(category, request.Attributes);
        if (attributeErrors.Count > 0)
        {
            return InvalidAttributes(attributeErrors);
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

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
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

        dbContext.Stocks.Remove(stock);
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

    private Task<Category?> GetActiveCategoryAsync(int categoryId, CancellationToken cancellationToken) =>
        dbContext.Categories
            .AsNoTracking()
            .SingleOrDefaultAsync(category => category.Id == categoryId && category.IsActive, cancellationToken);

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
        stock.TotalQuantity = request.TotalQuantity!.Value;
        stock.MinOrderQuantity = request.MinOrderQuantity!.Value;
        stock.StepQuantity = request.StepQuantity!.Value;
        stock.Unit = request.Unit;
        stock.PackagingType = request.PackagingType.Trim();
        stock.PricePerUnit = request.PricePerUnit!.Value;
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

    private BadRequestObjectResult InvalidAttributes(IReadOnlyList<string> attributeErrors) => BadRequest(
        new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            [nameof(StockContentRequest.Attributes)] = attributeErrors.ToArray()
        }));
}
