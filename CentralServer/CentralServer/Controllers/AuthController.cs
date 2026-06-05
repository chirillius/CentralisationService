using CentralServer.Models;
using CentralServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace CentralServer.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly AccessStoreService _accessStoreService;
    private readonly CompanyAccessContextService _contextService;

    public AuthController(AccessStoreService accessStoreService, CompanyAccessContextService contextService)
    {
        _accessStoreService = accessStoreService;
        _contextService = contextService;
    }

    [HttpPost("activate-invitation")]
    public async Task<IActionResult> ActivateInvitation([FromBody] ActivateInvitationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _accessStoreService.ActivateInvitationAsync(request, cancellationToken);
            return Ok(ToResponse(result.SessionToken, result.Context));
        }
        catch (AccessDeniedException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = ex.Code, message = ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _accessStoreService.LoginAsync(request, cancellationToken);
            return Ok(ToResponse(result.SessionToken, result.Context));
        }
        catch (AccessDeniedException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = ex.Code, message = ex.Message });
        }
    }

    [HttpGet("me")]
    public IActionResult Me()
    {
        try
        {
            return Ok(ToResponse(null, _contextService.RequireCurrent()));
        }
        catch (AccessDeniedException ex)
        {
            return Unauthorized(new { code = ex.Code, message = ex.Message });
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var current = _contextService.RequireCurrent();
        await _accessStoreService.RevokeSessionAsync(current.SessionId, cancellationToken);
        return NoContent();
    }

    private static object ToResponse(string? sessionToken, AuthenticatedCompanyContext context) => new
    {
        sessionToken,
        account = new
        {
            id = context.AccountId,
            context.Login,
            context.DisplayName,
            context.RoleKey,
            context.Permissions,
            context.AccessExpiresAtUtc,
        },
        company = new
        {
            id = context.CompanyId,
            key = context.CompanyKey,
            name = context.CompanyName,
        },
    };
}
