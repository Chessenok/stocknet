using System.ComponentModel.DataAnnotations;

namespace StockNet.Api.Contracts.Messaging;

public sealed class CreateMessageThreadRequest
{
    [Required]
    public Guid? StockId { get; init; }
}
