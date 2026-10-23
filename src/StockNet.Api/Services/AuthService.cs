using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockNet.Api.Contracts.Auth;
using StockNet.Api.Domain.Entities;
using StockNet.Api.Infrastructure.Authentication;
using StockNet.Api.Infrastructure.Persistence;

namespace StockNet.Api.Services;

public sealed class AuthService(
    StockNetDbContext dbContext,
    IPasswordHasher<Company> passwordHasher,
    JwtTokenService jwtTokenService) : IAuthService
{
    public async Task<AuthSessionDto?> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var normalizedEmail = NormalizeEmail(email);

        var emailExists = await dbContext.Companies
            .AsNoTracking()
            .AnyAsync(company => company.NormalizedEmail == normalizedEmail, cancellationToken);

        if (emailExists)
        {
            return null;
        }

        if (request.CompanyType is not { } companyType || !Enum.IsDefined(companyType))
        {
            throw new ArgumentException("A valid company type is required.", nameof(request));
        }

        var company = new Company
        {
            Name = request.Name.Trim(),
            Email = email,
            NormalizedEmail = normalizedEmail,
            TaxId = request.TaxId.Trim(),
            CountryCode = request.CountryCode.Trim().ToUpperInvariant(),
            CompanyType = companyType,
            IsVerified = false,
            IsAdmin = false,
            IsSubscribed = false
        };
        company.PasswordHash = passwordHasher.HashPassword(company, request.Password);

        dbContext.Companies.Add(company);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsNormalizedEmailConflict(exception))
        {
            return null;
        }

        return jwtTokenService.CreateSession(company);
    }

    public async Task<AuthSessionDto?> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(request.Email.Trim());
        var company = await dbContext.Companies
            .SingleOrDefaultAsync(company => company.NormalizedEmail == normalizedEmail, cancellationToken);

        if (company is null)
        {
            return null;
        }

        var verification = passwordHasher.VerifyHashedPassword(company, company.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return null;
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            company.PasswordHash = passwordHasher.HashPassword(company, request.Password);
            company.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return jwtTokenService.CreateSession(company);
    }

    private static string NormalizeEmail(string email) => email.ToUpperInvariant();

    private static bool IsNormalizedEmailConflict(DbUpdateException exception) =>
        exception.GetBaseException() is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_Companies_NormalizedEmail"
        };
}
