using CentralServer.Models;
using CentralServer.Services;
using CentralisationService.Entities.Models.Zones;
using Microsoft.AspNetCore.Mvc;

namespace CentralServer.Controllers;

[ApiController]
[Route("api/zones")]
public sealed class ZonesController : ControllerBase
{
    private readonly ZoneCatalogService _zoneCatalogService;
    private readonly CompanyAccessContextService? _accessContextService;

    public ZonesController(ZoneCatalogService zoneCatalogService, CompanyAccessContextService? accessContextService = null)
    {
        _zoneCatalogService = zoneCatalogService;
        _accessContextService = accessContextService;
    }

    [HttpGet("names")]
    public async Task<IActionResult> GetZoneNames(CancellationToken cancellationToken)
    {
        var catalog = await _zoneCatalogService.GetZoneNameCatalogAsync(cancellationToken);
        return Ok(catalog);
    }

    [HttpGet]
    public async Task<IActionResult> GetZones([FromQuery] string cameraKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cameraKey))
        {
            return BadRequest(new { message = "Нужно указать технический ключ камеры." });
        }

        var company = _accessContextService?.RequireCurrent();
        if (company is not null && !cameraKey.StartsWith($"{company.CompanyKey}-", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }
        var zones = await _zoneCatalogService.GetZonesAsync(cameraKey, cancellationToken);
        return Ok(zones);
    }

    [HttpPost]
    public async Task<IActionResult> CreateZone([FromBody] UpsertZoneRequest request, CancellationToken cancellationToken)
    {
        if (_accessContextService is not null && !_accessContextService.CanAccessSite(request.SiteKey))
        {
            return NotFound();
        }
        var zone = await _zoneCatalogService.UpsertZoneAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetZones), new { cameraKey = zone.CameraKey }, zone);
    }

    [HttpPut("{zoneId:guid}")]
    public async Task<IActionResult> UpdateZone(Guid zoneId, [FromBody] UpsertZoneRequest request, CancellationToken cancellationToken)
    {
        if (_accessContextService is not null && !_accessContextService.CanAccessSite(request.SiteKey))
        {
            return NotFound();
        }
        var zone = await _zoneCatalogService.UpsertZoneAsync(
            new UpsertZoneRequest
            {
                Id = zoneId,
                SiteKey = request.SiteKey,
                CameraKey = request.CameraKey,
                ZoneTypeKey = request.ZoneTypeKey,
                ZoneName = request.ZoneName,
                CustomName = request.CustomName,
                Points = request.Points,
            },
            cancellationToken);

        return Ok(zone);
    }

    [HttpDelete("{zoneId:guid}")]
    public async Task<IActionResult> DeleteZone(Guid zoneId, CancellationToken cancellationToken)
    {
        var zone = await _zoneCatalogService.GetZoneAsync(zoneId, cancellationToken);
        if (zone is null || _accessContextService is not null && !_accessContextService.CanAccessSite(zone.SiteKey))
        {
            return NotFound();
        }
        var deleted = await _zoneCatalogService.DeleteZoneAsync(zoneId, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
