using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using StockNet.Api.Infrastructure.Persistence;

namespace StockNet.Api.Infrastructure.Authorization;

public sealed class IsAdminAuthorizationHandler(StockNetDbContext dbContext)
    : AuthorizationHandler<IsAdminRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        IsAdminRequirement requirement)
    {
        if (!CompanyClaims.TryGetCompanyId(context.User, out var companyId))
        {
            return;
        }

        var isAdmin = await dbContext.Companies
            .AsNoTracking()
            .AnyAsync(company => company.Id == companyId && company.IsAdmin);

        if (isAdmin)
        {
            context.Succeed(requirement);
        }
    }
}
