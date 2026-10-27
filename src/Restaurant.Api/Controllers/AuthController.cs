using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Restaurant.Api.Middleware;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Common.Options;
using Restaurant.Application.Features.Auth;

namespace Restaurant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;
    private readonly AuthOptions _authOptions;

    public AuthController(
        IAuthService authService,
        ILogger<AuthController> logger,
        IOptions<AuthOptions> authOptions)
    {
        _authService = authService;
        _logger = logger;
        _authOptions = authOptions.Value;
    }

    [AllowAnonymous]
    [EnableRateLimiting(AuthRateLimiting.RegisterPolicy)]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterTenantDto dto, CancellationToken cancellationToken)
    {
        if (!_authOptions.AllowPublicRegistration)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = "El registro público no está habilitado. Solicite un acceso al administrador." });
        }

        try
        {
            var result = await _authService.RegisterTenantAsync(dto, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [AllowAnonymous]
    [EnableRateLimiting(AuthRateLimiting.LoginPolicy)]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto dto, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Login attempt");
        try
        {
            var result = await _authService.LoginAsync(dto, cancellationToken);
            _logger.LogDebug("Login successful for email: {Email}", dto.Email);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Login failed for email: {Email} - {Message}", dto.Email, ex.Message);
            return Unauthorized(new { message = MapLoginErrorMessage(ex.Message) });
        }
        catch (MultipleTenantsLoginException ex)
        {
            _logger.LogInformation(
                "Login requires tenant selection for email: {Email} ({Count} tenants)",
                dto.Email,
                ex.Tenants.Count);
            return BadRequest(new
            {
                code = MultipleTenantsLoginException.ErrorCode,
                message = ex.Message,
                tenants = ex.Tenants,
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Login error for email: {Email} - {Message}", dto.Email, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }

    private static string MapLoginErrorMessage(string message) =>
        message switch
        {
            "Invalid credentials." => "Correo o contraseña incorrectos.",
            "No active tenant memberships." => "No tiene acceso activo a ningún local.",
            "Tenant not found for this account." => "Ese local no está asociado a su cuenta.",
            _ => message,
        };
}
