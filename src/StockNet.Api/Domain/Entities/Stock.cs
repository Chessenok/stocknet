using System.Text.Json;
using StockNet.Api.Domain.Enums;

namespace StockNet.Api.Domain.Entities;

public sealed class Stock
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WarehouseId { get; set; }
    public int CategoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal TotalQuantity { get; set; }
    public decimal MinOrderQuantity { get; set; }
    public decimal StepQuantity { get; set; }
    public UnitOfMeasure Unit { get; set; } = UnitOfMeasure.Tons;
    public string PackagingType { get; set; } = string.Empty;
    public decimal PricePerUnit { get; set; }
    public string Currency { get; set; } = "EUR";
    public JsonDocument? Attributes { get; set; }
    public DateTime AvailableFrom { get; set; }
    public DateTime AvailableTo { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Warehouse Warehouse { get; set; } = default!;
    public Category Category { get; set; } = default!;
    public ICollection<MessageThread> MessageThreads { get; set; } = new List<MessageThread>();
}
