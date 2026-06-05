using System.Security.Cryptography;
using System.Text;
using CentralServer.Models;
using CentralServer.Services;
using CentralisationService.Entities.Models.Access;
using CentralisationService.Entities.Models.Catalog;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CentralServer.Controllers;

[ApiController]
[Route("api/platform/companies")]
public sealed class PlatformCompaniesController : ControllerBase
{
    private readonly AccessStoreService _accessStoreService;
    private readonly ServerRegistryService _registryService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AccessOptions _options;

    public PlatformCompaniesController(
        AccessStoreService accessStoreService,
        ServerRegistryService registryService,
        IHttpClientFactory httpClientFactory,
        IOptions<AccessOptions> options)
    {
        _accessStoreService = accessStoreService;
        _registryService = registryService;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    [HttpGet]
    public async Task<IActionResult> GetCompanies(CancellationToken cancellationToken)
    {
        if (!await IsPlatformAdminAsync(cancellationToken))
        {
            return Unauthorized(new { code = "platform_admin_required" });
        }
        return Ok(await _accessStoreService.GetCompaniesAsync(cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> CreateCompany([FromBody] CreateCompanyRequest request, CancellationToken cancellationToken)
    {
        if (!await IsPlatformAdminAsync(cancellationToken))
        {
            return Unauthorized(new { code = "platform_admin_required" });
        }
        if (string.IsNullOrWhiteSpace(request.Key) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { code = "invalid_company", message = "Нужно указать технический ключ и название компании." });
        }

        var company = new CompanyAccessRecord
        {
            Id = Guid.NewGuid(),
            Key = request.Key.Trim(),
            Name = request.Name.Trim(),
            Status = CompanyStatus.Active,
            AccessExpiresAtUtc = request.AccessExpiresAtUtc,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        await _accessStoreService.UpsertCompanyAsync(company, cancellationToken);
        return CreatedAtAction(nameof(GetCompanies), company);
    }

    [HttpPut("{companyId:guid}/access")]
    public async Task<IActionResult> UpdateAccess(Guid companyId, [FromBody] UpdateCompanyAccessRequest request, CancellationToken cancellationToken)
    {
        if (!await IsPlatformAdminAsync(cancellationToken))
        {
            return Unauthorized(new { code = "platform_admin_required" });
        }
        var current = await _accessStoreService.GetCompanyAsync(companyId, cancellationToken);
        if (current is null)
        {
            return NotFound();
        }
        if (!Enum.TryParse<CompanyStatus>(request.Status, true, out var status))
        {
            return BadRequest(new { code = "invalid_company_status", message = "Некорректный статус компании." });
        }

        var updated = new CompanyAccessRecord
        {
            Id = current.Id,
            Key = current.Key,
            Name = current.Name,
            Status = status,
            AccessExpiresAtUtc = request.AccessExpiresAtUtc,
            DisabledAtUtc = status == CompanyStatus.Active ? null : DateTime.UtcNow,
            DisabledReason = status == CompanyStatus.Active ? null : request.Reason,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        await _accessStoreService.UpsertCompanyAsync(updated, cancellationToken);
        if (status != CompanyStatus.Active || updated.AccessExpiresAtUtc.HasValue && updated.AccessExpiresAtUtc <= DateTime.UtcNow)
        {
            await _accessStoreService.RevokeCompanySessionsAsync(companyId, cancellationToken);
        }
        return Ok(updated);
    }

    [HttpGet("{companyId:guid}/sites")]
    public async Task<IActionResult> GetSites(Guid companyId, CancellationToken cancellationToken)
    {
        if (!await IsPlatformAdminAsync(cancellationToken))
        {
            return Unauthorized(new { code = "platform_admin_required" });
        }
        var company = await _accessStoreService.GetCompanyAsync(companyId, cancellationToken);
        if (company is null)
        {
            return NotFound();
        }

        await _registryService.RefreshAsync(cancellationToken);
        return Ok(_registryService.GetServers(company.Key).Select(server => new
        {
            server.CompanyKey,
            server.SiteKey,
            server.SiteName,
            server.ServerBaseUrl,
            server.ConnectorId,
            server.CleaningDay,
            server.LastSyncUtc,
            server.IsAvailable,
            Cameras = server.Cameras.Select(camera => new
            {
                camera.CameraId,
                camera.CameraKey,
                camera.SourceCameraKey,
                camera.CameraName,
                camera.IsAvailable,
            }),
        }));
    }

    [HttpPost("{companyId:guid}/sites")]
    public async Task<IActionResult> BindSite(Guid companyId, [FromBody] BindCompanyServerRequest request, CancellationToken cancellationToken)
    {
        if (!await IsPlatformAdminAsync(cancellationToken))
        {
            return Unauthorized(new { code = "platform_admin_required" });
        }
        var company = await _accessStoreService.GetCompanyAsync(companyId, cancellationToken);
        if (company is null)
        {
            return NotFound();
        }
        if (!await _accessStoreService.IsCompanyActiveAsync(companyId, cancellationToken))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "company_unavailable" });
        }
        if (string.IsNullOrWhiteSpace(request.ServerAddress))
        {
            return BadRequest(new { code = "server_address_required", message = "Нужно указать адрес сервера точки." });
        }
        if (string.IsNullOrWhiteSpace(request.SiteName))
        {
            return BadRequest(new { code = "site_name_required", message = "Нужно указать корректное название точки." });
        }

        var normalizedBaseUrl = NormalizeServerBaseUrl(request.ServerAddress);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var siteKey = string.IsNullOrWhiteSpace(request.SiteKey)
            ? ($"site-{Guid.NewGuid():N}")[..17]
            : request.SiteKey.Trim();
        var siteName = request.SiteName.Trim();

        var registrationRequest = new ConnectorRegistrationRequest
        {
            CompanyId = company.Id,
            CompanyKey = company.Key,
            SiteKey = siteKey,
            SiteName = siteName,
            CentralServerUrl = $"{Request.Scheme}://{Request.Host}",
            ConnectorAccessToken = token,
        };

        var client = _httpClientFactory.CreateClient(nameof(PlatformCompaniesController));
        using var response = await client.PostAsJsonAsync($"{normalizedBaseUrl}/api/connector/register", registrationRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                code = "connector_registration_failed",
                message = "Не удалось зарегистрировать сервер точки.",
                status = (int)response.StatusCode,
            });
        }

        var binding = await _accessStoreService.UpsertCompanySiteAsync(new CompanySiteBinding
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CompanyKey = company.Key,
            SiteKey = siteKey,
            SiteName = siteName,
            ServerBaseUrl = normalizedBaseUrl,
            ConnectorAccessToken = token,
            CleaningDay = request.CleaningDay,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        }, cancellationToken);

        await _registryService.RefreshAsync(cancellationToken);
        return Ok(new
        {
            binding.Id,
            binding.CompanyId,
            binding.CompanyKey,
            binding.SiteKey,
            binding.SiteName,
            binding.ServerBaseUrl,
            binding.CleaningDay,
            binding.CreatedAtUtc,
            binding.UpdatedAtUtc,
        });
    }

    [HttpGet("{companyId:guid}/accounts")]
    public async Task<IActionResult> GetAccounts(Guid companyId, CancellationToken cancellationToken)
    {
        if (!await IsPlatformAdminAsync(cancellationToken))
        {
            return Unauthorized(new { code = "platform_admin_required" });
        }
        if (await _accessStoreService.GetCompanyAsync(companyId, cancellationToken) is null)
        {
            return NotFound();
        }
        return Ok(await _accessStoreService.GetCompanyAccountsAsync(companyId, cancellationToken));
    }

    [HttpGet("{companyId:guid}/invitations")]
    public async Task<IActionResult> GetInvitations(Guid companyId, CancellationToken cancellationToken)
    {
        if (!await IsPlatformAdminAsync(cancellationToken))
        {
            return Unauthorized(new { code = "platform_admin_required" });
        }
        if (await _accessStoreService.GetCompanyAsync(companyId, cancellationToken) is null)
        {
            return NotFound();
        }
        return Ok(await _accessStoreService.GetCompanyInvitationsAsync(companyId, cancellationToken));
    }

    [HttpPost("{companyId:guid}/invitations")]
    public async Task<IActionResult> CreateInvitation(Guid companyId, [FromBody] CreateInvitationRequest request, CancellationToken cancellationToken)
    {
        if (!await IsPlatformAdminAsync(cancellationToken))
        {
            return Unauthorized(new { code = "platform_admin_required" });
        }
        if (!await _accessStoreService.IsCompanyActiveAsync(companyId, cancellationToken))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "company_unavailable" });
        }

        var result = await _accessStoreService.CreateInvitationAsync(companyId, request, cancellationToken);
        return Ok(new
        {
            invitation = result.Invitation,
            token = result.Token,
            note = "The invitation token is shown only once.",
        });
    }

    [HttpPost("{companyId:guid}/invitations/revoke-active")]
    public async Task<IActionResult> RevokeActiveInvitations(Guid companyId, CancellationToken cancellationToken)
    {
        if (!await IsPlatformAdminAsync(cancellationToken))
        {
            return Unauthorized(new { code = "platform_admin_required" });
        }
        if (await _accessStoreService.GetCompanyAsync(companyId, cancellationToken) is null)
        {
            return NotFound();
        }

        await _accessStoreService.RevokeCompanyInvitationsAsync(companyId, cancellationToken);
        return NoContent();
    }

    private async Task<bool> IsPlatformAdminAsync(CancellationToken cancellationToken)
    {
        var bearerToken = GetBearerToken();
        if (!string.IsNullOrWhiteSpace(bearerToken)
            && await _accessStoreService.ResolvePlatformAdminSessionAsync(bearerToken, cancellationToken) is not null)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(_options.PlatformAdminKey))
        {
            var provided = Request.Headers["X-Platform-Admin-Key"].ToString();
            return CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(provided)),
                SHA256.HashData(Encoding.UTF8.GetBytes(_options.PlatformAdminKey)));
        }

        return false;
    }

    private string? GetBearerToken()
    {
        var authorization = Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        return authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? authorization[prefix.Length..].Trim()
            : null;
    }

    private static string NormalizeServerBaseUrl(string serverAddress)
    {
        var value = serverAddress.Trim().TrimEnd('/');
        if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            value = $"http://{value}";
        }

        var uri = new Uri(value);
        if (uri.IsDefaultPort)
        {
            var builder = new UriBuilder(uri)
            {
                Port = 5120,
            };
            return builder.Uri.ToString().TrimEnd('/');
        }

        return uri.ToString().TrimEnd('/');
    }
}
