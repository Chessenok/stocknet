using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockNet.Api.Contracts.Companies;
using StockNet.Api.Infrastructure.Authorization;
using StockNet.Api.Infrastructure.Persistence;

namespace StockNet.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/companies")]
public sealed class CompaniesController(StockNetDbContext dbContext) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType<CompanyProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyProfileDto>> GetMyProfile(CancellationToken cancellationToken)
    {
        var companyId = User.GetCompanyId();
        var profile = await dbContext.Companies
            .AsNoTracking()
            .Where(company => company.Id == companyId)
            .Select(company => new CompanyProfileDto(
                company.Id,
                company.Name,
                company.Email,
                company.TaxId,
                company.CountryCode,
                company.CompanyType,
                company.IsVerified,
                company.IsAdmin,
                company.IsSubscribed,
                company.SubscriptionExpiresAt,
                company.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken);

        return profile is null ? NotFound() : Ok(profile);
    }
}
