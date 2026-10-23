using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Npgsql;
using StockNet.Api.Contracts.Warehouses;
using StockNet.Api.Domain.Entities;
using StockNet.Api.Infrastructure.Authorization;
using StockNet.Api.Infrastructure.Persistence;

namespace StockNet.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/warehouses")]
public sealed class WarehousesController(StockNetDbContext dbContext) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<WarehouseDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<WarehouseDto>> Create(
        [FromBody] WarehouseUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var companyId = User.GetCompanyId();
        var now = DateTime.UtcNow;
        var warehouse = new Warehouse
        {
            CompanyId = companyId,
            Name = request.Name.Trim(),
            AddressText = request.AddressText.Trim(),
            Location = CreatePoint(request.Longitude!.Value, request.Latitude!.Value),
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Warehouses.Add(warehouse);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await GetWarehouseDtoAsync(warehouse.Id, companyId, cancellationToken);
        return Created("/api/warehouses", response);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<WarehouseDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WarehouseDto>>> ListMine(CancellationToken cancellationToken)
    {
        var companyId = User.GetCompanyId();
        var warehouseRows = await dbContext.Warehouses
            .AsNoTracking()
            .Where(warehouse => warehouse.CompanyId == companyId)
            .OrderBy(warehouse => warehouse.Name)
            .ThenBy(warehouse => warehouse.Id)
            .Select(WarehouseProjection.Projection)
            .ToListAsync(cancellationToken);

        return Ok(warehouseRows.Select(warehouse => warehouse.ToDto()).ToList());
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<WarehouseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WarehouseDto>> Update(
        Guid id,
        [FromBody] WarehouseUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var companyId = User.GetCompanyId();
        var warehouse = await dbContext.Warehouses
            .SingleOrDefaultAsync(
                item => item.Id == id && item.CompanyId == companyId,
                cancellationToken);

        if (warehouse is null)
        {
            return NotFound();
        }

        warehouse.Name = request.Name.Trim();
        warehouse.AddressText = request.AddressText.Trim();
        warehouse.Location = CreatePoint(request.Longitude!.Value, request.Latitude!.Value);
        warehouse.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await GetWarehouseDtoAsync(id, companyId, cancellationToken);
        return Ok(response);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var companyId = User.GetCompanyId();
        var warehouse = await dbContext.Warehouses
            .SingleOrDefaultAsync(
                item => item.Id == id && item.CompanyId == companyId,
                cancellationToken);

        if (warehouse is null)
        {
            return NotFound();
        }

        if (await dbContext.Stocks.AnyAsync(stock => stock.WarehouseId == id, cancellationToken))
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Warehouse cannot be deleted while it contains stock."
            });
        }

        dbContext.Warehouses.Remove(warehouse);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsForeignKeyViolation(exception))
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Warehouse cannot be deleted while it is referenced by stock."
            });
        }

        return NoContent();
    }

    private async Task<WarehouseDto?> GetWarehouseDtoAsync(
        Guid id,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var row = await dbContext.Warehouses
            .AsNoTracking()
            .Where(warehouse => warehouse.Id == id && warehouse.CompanyId == companyId)
            .Select(WarehouseProjection.Projection)
            .SingleOrDefaultAsync(cancellationToken);

        return row?.ToDto();
    }

    private static Point CreatePoint(double longitude, double latitude) =>
        new(longitude, latitude) { SRID = 4326 };

    private static bool IsForeignKeyViolation(DbUpdateException exception) =>
        exception.GetBaseException() is PostgresException postgresException
        && postgresException.SqlState == PostgresErrorCodes.ForeignKeyViolation;
}
