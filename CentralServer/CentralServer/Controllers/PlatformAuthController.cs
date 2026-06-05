using CentralServer.Models;
using CentralServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace CentralServer.Controllers;

[ApiController]
[Route("api/platform/auth")]
public sealed class PlatformAuthController : ControllerBase
{
    private readonly AccessStoreService _accessStoreService;

    public PlatformAuthController(AccessStoreService accessStoreService)
    {
        _accessStoreService = accessStoreService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] PlatformAdminLoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _accessStoreService.LoginPlatformAdminAsync(request, cancellationToken);
            return Ok(new
            {
                platformSessionToken = result.SessionToken,
                admin = new
                {
                    login = result.Session.Login,
                    displayName = "Platform Administrator",
                    roleKey = "platform-admin",
                },
                expiresAtUtc = result.Session.ExpiresAtUtc,
            });
        }
        catch (AccessDeniedException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = ex.Code, message = ex.Message });
        }
    }
}
