using Microsoft.AspNetCore.Mvc;
using CentralServer.Services;

namespace CentralServer.Controllers;

[ApiController]
[Route("api/cameras")]
public sealed class CamerasController : ControllerBase
{
    private readonly ServerRegistryService _registryService;
    private readonly RemoteFrameProxyService _frameProxyService;
    private readonly CompanyAccessContextService _accessContextService;

    public CamerasController(ServerRegistryService registryService, RemoteFrameProxyService frameProxyService, CompanyAccessContextService accessContextService)
    {
        _registryService = registryService;
        _frameProxyService = frameProxyService;
        _accessContextService = accessContextService;
    }

    [HttpGet]
    public async Task<IActionResult> GetCameras([FromQuery] string? siteKey, CancellationToken cancellationToken)
    {
        await _registryService.EnsureSynchronizedAsync(cancellationToken);
        var company = _accessContextService.RequireCurrent();

        return Ok(_registryService.GetAllCameras(siteKey, company.CompanyKey).Select(camera => new
        {
            key = camera.CameraKey,
            name = camera.CameraName,
            siteKey = camera.SiteKey,
            siteName = camera.SiteName,
            cameraId = camera.CameraId,
            sourceCameraKey = camera.SourceCameraKey,
            serverBaseUrl = camera.ServerBaseUrl,
            lastSyncUtc = camera.LastSyncUtc,
            isAvailable = camera.IsAvailable,
        }));
    }

    [HttpGet("{cameraKey}/frame")]
    public async Task<IActionResult> GetFrame(string cameraKey, CancellationToken cancellationToken)
    {
        await _registryService.EnsureSynchronizedAsync(cancellationToken);
        var company = _accessContextService.RequireCurrent();

        var camera = _registryService.GetCamera(cameraKey, company.CompanyKey);
        if (camera is null)
        {
            return NotFound(new { message = $"Камера '{cameraKey}' не настроена на Центральном сервере." });
        }

        var frame = await _frameProxyService.GetFrameAsync(camera, cancellationToken);
        return File(frame, "image/jpeg");
    }
}
