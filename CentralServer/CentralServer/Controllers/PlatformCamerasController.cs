using CentralServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace CentralServer.Controllers;

[ApiController]
[Route("api/platform/cameras")]
public sealed class PlatformCamerasController : ControllerBase
{
    private readonly ServerRegistryService _registryService;
    private readonly RemoteFrameProxyService _frameProxyService;
    private readonly PlatformAdminAccessService _platformAdminAccessService;

    public PlatformCamerasController(
        ServerRegistryService registryService,
        RemoteFrameProxyService frameProxyService,
        PlatformAdminAccessService platformAdminAccessService)
    {
        _registryService = registryService;
        _frameProxyService = frameProxyService;
        _platformAdminAccessService = platformAdminAccessService;
    }

    [HttpGet("{cameraKey}/frame")]
    public async Task<IActionResult> GetFrame(string cameraKey, CancellationToken cancellationToken)
    {
        if (!await _platformAdminAccessService.IsPlatformAdminAsync(HttpContext, cancellationToken))
        {
            return Unauthorized(new { code = "platform_admin_required" });
        }

        await _registryService.EnsureSynchronizedAsync(cancellationToken);
        var camera = _registryService.GetAllCameras(null, null)
            .FirstOrDefault(item => string.Equals(item.CameraKey, cameraKey, StringComparison.OrdinalIgnoreCase));
        if (camera is null)
        {
            return NotFound(new { message = $"Камера '{cameraKey}' не настроена на Центральном сервере." });
        }

        var frame = await _frameProxyService.GetFrameAsync(camera, cancellationToken);
        return File(frame, "image/jpeg");
    }
}
