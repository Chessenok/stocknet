using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniExcelLibs;
using StockNet.Api.Contracts.Stocks;
using StockNet.Api.Domain.Entities;
using StockNet.Api.Infrastructure.Authorization;
using StockNet.Api.Infrastructure.Persistence;
using StockNet.Api.Services;

namespace StockNet.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/stocks")]
public sealed class StockImportController(
    StockNetDbContext dbContext,
    StockSubscriptionGuard subscriptionGuard,
    ILogger<StockImportController> logger) : ControllerBase
{
    private const long MaxImportFileBytes = 10 * 1024 * 1024;
    private const int MaxImportedRows = 1000;

    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxImportFileBytes + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxImportFileBytes)]
    [ProducesResponseType<StockImportResultDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockImportResultDto>> Import(
        [FromForm] ImportStocksRequest request,
        CancellationToken cancellationToken)
    {
        var companyId = User.GetCompanyId();
        if (!await subscriptionGuard.CanPostStockAsync(companyId, cancellationToken))
        {
            return SubscriptionRequired();
        }

        if (request.File is null || request.File.Length == 0)
        {
            return BadRequest(ImportProblem("A non-empty .xlsx file is required."));
        }

        var extension = Path.GetExtension(Path.GetFileName(request.File.FileName));
        if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(ImportProblem("Only .xlsx workbooks are supported."));
        }

        if (request.File.Length > MaxImportFileBytes)
        {
            return BadRequest(ImportProblem("The workbook exceeds the 10 MB import limit."));
        }

        var warehouseId = request.WarehouseId!.Value;
        var ownsWarehouse = await dbContext.Warehouses
            .AsNoTracking()
            .AnyAsync(warehouse => warehouse.Id == warehouseId && warehouse.CompanyId == companyId, cancellationToken);
        if (!ownsWarehouse)
        {
            return NotFound();
        }

        List<StockImportRow> rows;
        try
        {
            await using var stream = request.File.OpenReadStream();
            // MiniExcel 1.46 exposes a streaming synchronous query API. Keep its parsing off the
            // request thread and cap enumeration so oversized workbooks do not exhaust memory.
            rows = await Task.Run(
                () => MiniExcel.Query<StockImportRow>(stream, hasHeader: true)
                    .Take(MaxImportedRows + 1)
                    .ToList(),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning("Stock workbook could not be parsed. ExceptionType: {ExceptionType}", exception.GetType().Name);
            return BadRequest(ImportProblem("The workbook is invalid or could not be parsed."));
        }

        if (rows.Count == 0)
        {
            return BadRequest(ImportProblem("The workbook contains no stock rows."));
        }

        if (rows.Count > MaxImportedRows)
        {
            return BadRequest(ImportProblem($"A workbook may contain at most {MaxImportedRows} stock rows."));
        }

        var errors = ValidateRows(rows);
        var categoryIds = rows.Select(row => row.CategoryId).Where(id => id > 0).Distinct().ToArray();
        var activeCategories = await dbContext.Categories
            .AsNoTracking()
            .Where(category => category.IsActive && categoryIds.Contains(category.Id))
            .Select(category => category.Id)
            .ToHashSetAsync(cancellationToken);

        for (var index = 0; index < rows.Count; index++)
        {
            if (!activeCategories.Contains(rows[index].CategoryId))
            {
                AddImportError(errors, index + 2, nameof(StockImportRow.CategoryId), "CategoryId is missing or inactive.");
            }
        }

        if (errors.Count > 0)
        {
            return BadRequest(new ValidationProblemDetails(
                errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray())));
        }

        if (!await subscriptionGuard.CanPostStockAsync(companyId, cancellationToken))
        {
            return SubscriptionRequired();
        }

        var createdAt = DateTime.UtcNow;
        var stockItems = new List<Stock>(rows.Count);
        var attributeDocuments = new List<JsonDocument>();
        try
        {
            foreach (var row in rows)
            {
                JsonDocument? attributes = null;
                if (!string.IsNullOrWhiteSpace(row.AttributesJson))
                {
                    attributes = JsonDocument.Parse(row.AttributesJson);
                    attributeDocuments.Add(attributes);
                }

                stockItems.Add(new Stock
                {
                    WarehouseId = warehouseId,
                    CategoryId = row.CategoryId,
                    Title = row.Title.Trim(),
                    Description = row.Description.Trim(),
                    TotalQuantityTons = row.TotalQuantityTons!.Value,
                    MinOrderQuantityTons = row.MinOrderQuantityTons!.Value,
                    StepQuantityTons = row.StepQuantityTons!.Value,
                    PackagingType = row.PackagingType.Trim(),
                    PricePerTon = row.PricePerTon!.Value,
                    Currency = row.Currency.Trim().ToUpperInvariant(),
                    Attributes = attributes,
                    AvailableFrom = AsUtc(row.AvailableFrom!.Value),
                    AvailableTo = AsUtc(row.AvailableTo!.Value),
                    IsActive = true,
                    CreatedAt = createdAt,
                    UpdatedAt = createdAt
                });
            }

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            if (!await subscriptionGuard.CanPostStockAsync(companyId, cancellationToken))
            {
                return SubscriptionRequired();
            }

            dbContext.Stocks.AddRange(stockItems);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            foreach (var attributes in attributeDocuments)
            {
                attributes.Dispose();
            }
        }

        return StatusCode(StatusCodes.Status201Created, new StockImportResultDto(
            stockItems.Count,
            stockItems.Select(stock => stock.Id).ToArray()));
    }

    private static Dictionary<string, List<string>> ValidateRows(IReadOnlyList<StockImportRow> rows)
    {
        var errors = new Dictionary<string, List<string>>();
        for (var index = 0; index < rows.Count; index++)
        {
            var rowNumber = index + 2;
            var validationResults = new List<ValidationResult>();
            Validator.TryValidateObject(rows[index], new ValidationContext(rows[index]), validationResults, validateAllProperties: true);
            foreach (var validationResult in validationResults)
            {
                var members = validationResult.MemberNames.DefaultIfEmpty("Row");
                foreach (var member in members)
                {
                    AddImportError(errors, rowNumber, member, validationResult.ErrorMessage ?? "The row is invalid.");
                }
            }
        }

        return errors;
    }

    private static void AddImportError(Dictionary<string, List<string>> errors, int rowNumber, string member, string message)
    {
        var key = $"Rows[{rowNumber}].{member}";
        if (!errors.TryGetValue(key, out var messages))
        {
            messages = [];
            errors.Add(key, messages);
        }

        messages.Add(message);
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static ProblemDetails ImportProblem(string title) => new()
    {
        Status = StatusCodes.Status400BadRequest,
        Title = title
    };

    private ObjectResult SubscriptionRequired() => StatusCode(
        StatusCodes.Status403Forbidden,
        new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "An active subscription is required to import stock."
        });
}
