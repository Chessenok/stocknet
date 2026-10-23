using System.Security.Claims;

namespace StockNet.Api.Infrastructure.Authorization;

public static class CompanyClaims
{
    public const string CompanyId = "CompanyId";

    public static bool TryGetCompanyId(ClaimsPrincipal principal, out Guid companyId)
    {
        var value = principal.FindFirstValue(CompanyId)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");

        return Guid.TryParse(value, out companyId);
    }

    public static Guid GetCompanyId(this ClaimsPrincipal principal)
    {
        if (!TryGetCompanyId(principal, out var companyId))
        {
            throw new UnauthorizedAccessException("The authenticated principal does not contain a valid CompanyId.");
        }

        return companyId;
    }
}
