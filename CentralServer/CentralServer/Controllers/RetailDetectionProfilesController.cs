using CentralServer.Models;
using CentralServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace CentralServer.Controllers;

[ApiController]
[Route("api/detection-profiles")]
public sealed class RetailDetectionProfilesController : ControllerBase
{
    private readonly RetailDetectionProfileCatalogService _profileCatalogService;
    private readonly CompanyAccessContextService? _accessContextService;

    public RetailDetectionProfilesController(
        RetailDetectionProfileCatalogService profileCatalogService,
        CompanyAccessContextService? accessContextService = null)
    {
        _profileCatalogService = profileCatalogService;
        _accessContextService = accessContextService;
    }

    [HttpGet]
    public async Task<IActionResult> GetProfiles(CancellationToken cancellationToken)
    {
        var profiles = await _profileCatalogService.GetProfilesAsync(cancellationToken);
        var company = _accessContextService?.RequireCurrent();
        return Ok(company is null
            ? profiles
            : profiles.Where(profile => profile.CameraKey.StartsWith($"{company.CompanyKey}-", StringComparison.OrdinalIgnoreCase)));
    }

    [HttpPut("{profileKey}")]
    public async Task<IActionResult> UpsertProfile(
        string profileKey,
        [FromBody] ConfiguredRetailDetectionProfile profile,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(profileKey))
        {
            return BadRequest(new { message = "Нужно указать технический ключ профиля." });
        }
        var company = _accessContextService?.RequireCurrent();
        if (company is not null && !profile.CameraKey.StartsWith($"{company.CompanyKey}-", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        var saved = await _profileCatalogService.UpsertProfileAsync(
            new ConfiguredRetailDetectionProfile
            {
                Id = profile.Id,
                ProfileKey = profileKey,
                Name = profile.Name,
                CameraKey = profile.CameraKey,
                DetectionTypeKey = profile.DetectionTypeKey,
                IsEnabled = profile.IsEnabled,
                ClientZoneTypeKey = profile.ClientZoneTypeKey,
                TargetZoneTypeKey = profile.TargetZoneTypeKey,
                RequiresClientZonePresence = profile.RequiresClientZonePresence,
                SaveEvidenceOnPositiveResult = profile.SaveEvidenceOnPositiveResult,
                IntervalSeconds = profile.IntervalSeconds,
                CooldownSeconds = profile.CooldownSeconds,
                ConfidenceThreshold = profile.ConfidenceThreshold,
            },
            cancellationToken);

        return Ok(saved);
    }
}
