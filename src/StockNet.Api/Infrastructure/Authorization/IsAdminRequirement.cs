using Microsoft.AspNetCore.Authorization;

namespace StockNet.Api.Infrastructure.Authorization;

public sealed class IsAdminRequirement : IAuthorizationRequirement
{
}
