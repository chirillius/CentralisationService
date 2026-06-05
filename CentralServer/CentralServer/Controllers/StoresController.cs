using Microsoft.AspNetCore.Mvc;
using CentralServer.Services;

namespace CentralServer.Controllers;

[ApiController]
[Route("api/stores")]
public sealed class StoresController : ControllerBase
{
    private readonly ServerRegistryService _registryService;
    private readonly CompanyAccessContextService _accessContextService;

    public StoresController(ServerRegistryService registryService, CompanyAccessContextService accessContextService)
    {
        _registryService = registryService;
        _accessContextService = accessContextService;
    }

    [HttpGet]
    public async Task<IActionResult> GetStores(CancellationToken cancellationToken)
    {
        await _registryService.EnsureSynchronizedAsync(cancellationToken);
        var company = _accessContextService.RequireCurrent();

        return Ok(_registryService.GetServers(company.CompanyKey).Select(store => new
        {
            store.SiteKey,
            store.SiteName,
            store.ServerBaseUrl,
            store.ConnectorId,
            store.CleaningDay,
            store.LastSyncUtc,
            store.IsAvailable,
            cameraCount = store.Cameras.Count,
        }));
    }
}
