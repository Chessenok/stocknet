using StockNet.Api.Domain.Enums;

namespace StockNet.Api.Domain.Entities;

public sealed class Company
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? TaxId { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public CompanyType CompanyType { get; set; }
    public bool IsVerified { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsSubscribed { get; set; }
    public DateTime? SubscriptionExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }

    public ICollection<Warehouse> Warehouses { get; set; } = new List<Warehouse>();
    public ICollection<MessageThread> BuyerMessageThreads { get; set; } = new List<MessageThread>();
    public ICollection<MessageThread> SellerMessageThreads { get; set; } = new List<MessageThread>();
    public ICollection<Message> SentMessages { get; set; } = new List<Message>();
}
