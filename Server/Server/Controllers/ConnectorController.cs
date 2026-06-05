using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Server.Models;
using Server.Services;

namespace Server.Controllers;

[ApiController]
[Route("api/connector")]
public sealed class ConnectorController : ControllerBase
{
    private readonly ServerNodeOptions _options;
    private readonly ConnectorBindingService _bindingService;

    public ConnectorController(IOptions<ServerNodeOptions> options, ConnectorBindingService bindingService)
    {
        _options = options.Value;
        _bindingService = bindingService;
    }

    [HttpGet("info")]
    public async Task<IActionResult> GetInfo(CancellationToken cancellationToken)
    {
        if (!await _bindingService.IsRequestAuthorizedAsync(Request, cancellationToken))
        {
            return Unauthorized(new { code = "connector_token_required", message = "Нужен токен доступа коннектора." });
        }

        var binding = await _bindingService.GetBindingAsync(cancellationToken);
        return Ok(new
        {
            connectorId = _options.ConnectorId,
            siteKey = string.IsNullOrWhiteSpace(binding?.SiteKey) ? _options.SiteKey : binding.SiteKey,
            siteName = string.IsNullOrWhiteSpace(binding?.SiteName) ? _options.SiteName : binding.SiteName,
            companyKey = binding?.CompanyKey,
            centralServerUrl = binding?.CentralServerUrl,
            registeredAtUtc = binding?.RegisteredAtUtc,
            cameraCount = _options.Cameras.Count,
            processingMode = "transport-only",
        });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] ConnectorRegistrationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var binding = await _bindingService.RegisterAsync(request, cancellationToken);
            return Ok(new
            {
                binding.CompanyId,
                binding.CompanyKey,
                binding.SiteKey,
                binding.SiteName,
                binding.CentralServerUrl,
                binding.RegisteredAtUtc,
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "invalid_connector_registration", message = ex.Message });
        }
    }
}
