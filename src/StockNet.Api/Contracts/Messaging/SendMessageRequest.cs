using System.ComponentModel.DataAnnotations;

namespace StockNet.Api.Contracts.Messaging;

public sealed class SendMessageRequest
{
    [Required, StringLength(5000)]
    public string Text { get; init; } = string.Empty;
}
