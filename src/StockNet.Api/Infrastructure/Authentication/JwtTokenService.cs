using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using StockNet.Api.Contracts.Auth;
using StockNet.Api.Domain.Entities;
using StockNet.Api.Infrastructure.Authorization;

namespace StockNet.Api.Infrastructure.Authentication;

public sealed class JwtTokenService(IConfiguration configuration)
{
    public AuthSessionDto CreateSession(Company company)
    {
        var issuer = configuration["Jwt:Issuer"]!;
        var audience = configuration["Jwt:Audience"]!;
        var signingKey = configuration["Jwt:SigningKey"]!;
        var expirationMinutes = configuration.GetValue("Jwt:ExpirationMinutes", 60);
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(expirationMinutes);

        var claims = new[]
        {
            new Claim(CompanyClaims.CompanyId, company.Id.ToString()),
            new Claim("IsAdmin", company.IsAdmin ? "true" : "false"),
            new Claim(JwtRegisteredClaimNames.Sub, company.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: credentials);

        return new AuthSessionDto(
            company.Id,
            new JwtSecurityTokenHandler().WriteToken(jwt),
            "Bearer",
            expiresAt,
            company.IsAdmin);
    }
}
