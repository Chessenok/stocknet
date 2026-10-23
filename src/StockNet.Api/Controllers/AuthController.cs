using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockNet.Api.Contracts.Auth;
using StockNet.Api.Services;

namespace StockNet.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType<AuthSessionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthSessionDto>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var session = await authService.RegisterAsync(request, cancellationToken);
        if (session is null)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "An account with this email already exists."
            });
        }

        return Created("/api/companies/me", session);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<AuthSessionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthSessionDto>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var session = await authService.LoginAsync(request, cancellationToken);
        if (session is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Invalid email or password."
            });
        }

        return Ok(session);
    }
}
