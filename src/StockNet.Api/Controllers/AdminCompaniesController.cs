using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockNet.Api.Contracts.Companies;
using StockNet.Api.Infrastructure.Authorization;
using StockNet.Api.Infrastructure.Persistence;

namespace StockNet.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/admin/companies")]
public sealed class AdminCompaniesController(StockNetDbContext dbContext) : ControllerBase
{
    [HttpPut("{id:guid}/subscription")]
    [ProducesResponseType<CompanySubscriptionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanySubscriptionDto>> OverrideSubscription(
        Guid id,
        [FromBody] ManualSubscriptionOverrideRequest request,
        CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (company is null)
        {
            return NotFound();
        }

        company.IsSubscribed = request.IsSubscribed!.Value;
        company.SubscriptionExpiresAt = request.SubscriptionExpiresAt?.UtcDateTime;
        company.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new CompanySubscriptionDto(
            company.Id,
            company.IsSubscribed,
            company.SubscriptionExpiresAt));
    }
}
