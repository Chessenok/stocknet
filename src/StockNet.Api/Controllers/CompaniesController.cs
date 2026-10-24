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

    [HttpPut("me")]
    [ProducesResponseType<CompanyProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyProfileDto>> UpdateMyProfile(
        [FromBody] UpdateCompanyProfileRequest request,
        CancellationToken cancellationToken)
    {
        var companyId = User.GetCompanyId();
        var company = await dbContext.Companies
            .SingleOrDefaultAsync(item => item.Id == companyId, cancellationToken);

        if (company is null)
        {
            return NotFound();
        }

        company.Name = request.Name.Trim();
        company.TaxId = request.TaxId?.Trim();
        company.CountryCode = request.CountryCode.Trim().ToUpperInvariant();
        company.CompanyType = request.CompanyType!.Value;
        company.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        var profile = await dbContext.Companies
            .AsNoTracking()
            .Where(item => item.Id == companyId)
            .Select(item => new CompanyProfileDto(
                item.Id,
                item.Name,
                item.Email,
                item.TaxId,
                item.CountryCode,
                item.CompanyType,
                item.IsVerified,
                item.IsAdmin,
                item.IsSubscribed,
                item.SubscriptionExpiresAt,
                item.CreatedAt))
            .SingleAsync(cancellationToken);

        return Ok(profile);
    }
}
