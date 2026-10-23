using System.Linq.Expressions;
using StockNet.Api.Domain.Entities;

namespace StockNet.Api.Contracts.Messaging;

public sealed record MessageThreadDto(
    Guid Id,
    Guid StockId,
    string StockTitle,
    Guid BuyerCompanyId,
    Guid SellerCompanyId,
    DateTime CreatedAt)
{
    public static Expression<Func<MessageThread, MessageThreadDto>> Projection => thread => new MessageThreadDto(
        thread.Id,
        thread.StockId,
        thread.Stock.Title,
        thread.BuyerCompanyId,
        thread.SellerCompanyId,
        thread.CreatedAt);
}
