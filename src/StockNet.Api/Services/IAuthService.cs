using StockNet.Api.Contracts.Auth;

namespace StockNet.Api.Services;

public interface IAuthService
{
    Task<AuthSessionDto?> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<AuthSessionDto?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
}
