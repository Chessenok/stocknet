namespace StockNet.Api.Domain.Entities;

public sealed class Category
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public string[] RequiredAttributeKeys { get; set; } = [];

    public Category? Parent { get; set; }
    public ICollection<Category> Children { get; set; } = new List<Category>();
    public ICollection<Stock> Stocks { get; set; } = new List<Stock>();
}
