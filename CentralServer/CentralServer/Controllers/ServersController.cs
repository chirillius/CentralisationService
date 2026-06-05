using Microsoft.AspNetCore.Mvc;
using CentralServer.Services;

namespace CentralServer.Controllers;

[ApiController]
[Route("api/servers")]
public sealed class ServersController : ControllerBase
{
    private readonly ServerRegistryService _registryService;

    public ServersController(ServerRegistryService registryService)
    {
        _registryService = registryService;
    }

    [HttpGet]
    public async Task<IActionResult> GetServers(CancellationToken cancellationToken)
    {
        await _registryService.EnsureSynchronizedAsync(cancellationToken);

        return Ok(_registryService.GetServers().Select(server => new
        {
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
}
