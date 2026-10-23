using Microsoft.EntityFrameworkCore;
using StockNet.Api.Infrastructure.Persistence;

namespace StockNet.Api.Services;

public sealed class StockSubscriptionGuard(StockNetDbContext dbContext)
{
    public Task<bool> CanPostStockAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return dbContext.Companies
            .AsNoTracking()
            .AnyAsync(
                company => company.Id == companyId
                    && company.IsSubscribed
                    && (company.SubscriptionExpiresAt == null || company.SubscriptionExpiresAt > now),
                cancellationToken);
    }
}
