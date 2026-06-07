using CentralServer.Models;
using CentralServer.Services;
using CentralisationService.Entities.Models.Zones;
using Microsoft.AspNetCore.Mvc;

namespace CentralServer.Controllers;

[ApiController]
[Route("api/platform/zones")]
public sealed class PlatformZonesController : ControllerBase
{
    private readonly ZoneCatalogService _zoneCatalogService;
    private readonly PlatformAdminAccessService _platformAdminAccessService;

    public PlatformZonesController(ZoneCatalogService zoneCatalogService, PlatformAdminAccessService platformAdminAccessService)
    {
        _zoneCatalogService = zoneCatalogService;
        _platformAdminAccessService = platformAdminAccessService;
    }

    [HttpGet("names")]
    public async Task<IActionResult> GetZoneNames(CancellationToken cancellationToken)
    {
        if (!await _platformAdminAccessService.IsPlatformAdminAsync(HttpContext, cancellationToken))
        {
            return Unauthorized(new { code = "platform_admin_required" });
        }

        return Ok(await _zoneCatalogService.GetZoneNameCatalogAsync(cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> GetZones([FromQuery] string cameraKey, CancellationToken cancellationToken)
    {
        if (!await _platformAdminAccessService.IsPlatformAdminAsync(HttpContext, cancellationToken))
        {
            return Unauthorized(new { code = "platform_admin_required" });
        }
        if (string.IsNullOrWhiteSpace(cameraKey))
        {
            return BadRequest(new { message = "Нужно указать технический ключ камеры." });
        }

        return Ok(await _zoneCatalogService.GetZonesAsync(cameraKey, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> CreateZone([FromBody] UpsertZoneRequest request, CancellationToken cancellationToken)
    {
        if (!await _platformAdminAccessService.IsPlatformAdminAsync(HttpContext, cancellationToken))
        {
            return Unauthorized(new { code = "platform_admin_required" });
        }

        var zone = await _zoneCatalogService.UpsertZoneAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetZones), new { cameraKey = zone.CameraKey }, zone);
    }

    [HttpPut("{zoneId:guid}")]
    public async Task<IActionResult> UpdateZone(Guid zoneId, [FromBody] UpsertZoneRequest request, CancellationToken cancellationToken)
    {
        if (!await _platformAdminAccessService.IsPlatformAdminAsync(HttpContext, cancellationToken))
        {
            return Unauthorized(new { code = "platform_admin_required" });
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
        if (!await _platformAdminAccessService.IsPlatformAdminAsync(HttpContext, cancellationToken))
        {
            return Unauthorized(new { code = "platform_admin_required" });
        }

        var deleted = await _zoneCatalogService.DeleteZoneAsync(zoneId, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
