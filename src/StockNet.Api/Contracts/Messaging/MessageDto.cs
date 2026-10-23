using System.Linq.Expressions;
using StockNet.Api.Domain.Entities;

namespace StockNet.Api.Contracts.Messaging;

public sealed record MessageDto(
    Guid Id,
    Guid ThreadId,
    Guid SenderCompanyId,
    string Text,
    DateTime SentAt)
{
    public static Expression<Func<Message, MessageDto>> Projection => message => new MessageDto(
        message.Id,
        message.ThreadId,
        message.SenderCompanyId,
        message.Text,
        message.SentAt);
}
