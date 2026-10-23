using NetTopologySuite.Geometries;

namespace StockNet.Api.Domain.Entities;

public sealed class Warehouse
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Point Location { get; set; } = default!;
    public string AddressText { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Company Company { get; set; } = default!;
    public ICollection<Stock> Stocks { get; set; } = new List<Stock>();
}
