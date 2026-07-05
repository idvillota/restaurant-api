using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Auth;

namespace Restaurant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterTenantDto dto, CancellationToken cancellationToken)
    {
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
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto dto, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Login attempt for email: {Email}", dto.Email);
        try
        {
            var result = await _authService.LoginAsync(dto, cancellationToken);
            _logger.LogInformation("Login successful for email: {Email}", dto.Email);
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
