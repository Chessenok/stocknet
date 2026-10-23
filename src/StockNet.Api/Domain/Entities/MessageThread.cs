namespace StockNet.Api.Domain.Entities;

public sealed class MessageThread
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StockId { get; set; }
    public Guid BuyerCompanyId { get; set; }
    public Guid SellerCompanyId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Stock Stock { get; set; } = default!;
    public Company BuyerCompany { get; set; } = default!;
    public Company SellerCompany { get; set; } = default!;
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
