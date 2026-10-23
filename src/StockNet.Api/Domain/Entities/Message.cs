namespace StockNet.Api.Domain.Entities;

public sealed class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ThreadId { get; set; }
    public Guid SenderCompanyId { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public MessageThread Thread { get; set; } = default!;
    public Company SenderCompany { get; set; } = default!;
}
