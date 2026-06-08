using Microsoft.AspNetCore.Mvc;
using CentralServer.Models;
using CentralServer.Services;
using CentralisationService.Entities.Models.Catalog;

namespace CentralServer.Controllers;

[ApiController]
[Route("api/cameras")]
public sealed class CamerasController : ControllerBase
{
    private readonly ServerRegistryService _registryService;
    private readonly RemoteFrameProxyService _frameProxyService;
    private readonly CompanyAccessContextService _accessContextService;
    private readonly AccessStoreService _accessStoreService;
    private readonly IHttpClientFactory _httpClientFactory;

    public CamerasController(
        ServerRegistryService registryService,
        RemoteFrameProxyService frameProxyService,
        CompanyAccessContextService accessContextService,
        AccessStoreService accessStoreService,
        IHttpClientFactory httpClientFactory)
    {
        _registryService = registryService;
        _frameProxyService = frameProxyService;
        _accessContextService = accessContextService;
        _accessStoreService = accessStoreService;
        _httpClientFactory = httpClientFactory;
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
            host = camera.Host,
            highQualityPath = camera.HighQualityPath,
            lowQualityPath = camera.LowQualityPath,
            serverBaseUrl = camera.ServerBaseUrl,
            lastSyncUtc = camera.LastSyncUtc,
            isAvailable = camera.IsAvailable,
        }));
    }

    [HttpPost("sites/{siteKey}")]
    public async Task<IActionResult> AddCamera(string siteKey, [FromBody] CameraConfigurationRequest request, CancellationToken cancellationToken)
    {
        var context = _accessContextService.RequireCurrent();
        var validation = await ValidateCompanyCameraMutationAsync(context, siteKey, request, cancellationToken);
        if (validation.Result is not null)
        {
            return validation.Result;
        }

        var serverCamera = await PushCameraToServerAsync(validation.Site!, request, existingCameraKey: null, cancellationToken);
        if (serverCamera.Result is not null)
        {
            return serverCamera.Result;
        }

        var saved = await _accessStoreService.UpsertCompanyCameraAsync(context.CompanyId, siteKey, ToRequest(serverCamera.Camera!), existingSourceCameraKey: null, cancellationToken);
        await _registryService.RefreshAsync(cancellationToken);
        return Ok(ToCameraResponse(saved ?? serverCamera.Camera!));
    }

    [HttpPut("sites/{siteKey}/{cameraKey}")]
    public async Task<IActionResult> UpdateCamera(string siteKey, string cameraKey, [FromBody] CameraConfigurationRequest request, CancellationToken cancellationToken)
    {
        var context = _accessContextService.RequireCurrent();
        var validation = await ValidateCompanyCameraMutationAsync(context, siteKey, request, cancellationToken);
        if (validation.Result is not null)
        {
            return validation.Result;
        }

        var serverCamera = await PushCameraToServerAsync(validation.Site!, request, cameraKey, cancellationToken);
        if (serverCamera.Result is not null)
        {
            return serverCamera.Result;
        }

        var saved = await _accessStoreService.UpsertCompanyCameraAsync(context.CompanyId, siteKey, ToRequest(serverCamera.Camera!), cameraKey, cancellationToken);
        await _registryService.RefreshAsync(cancellationToken);
        return Ok(ToCameraResponse(saved ?? serverCamera.Camera!));
    }

    [HttpDelete("sites/{siteKey}/{cameraKey}")]
    public async Task<IActionResult> DeleteCamera(string siteKey, string cameraKey, CancellationToken cancellationToken)
    {
        var context = _accessContextService.RequireCurrent();
        if (!_accessContextService.HasPermission("zones.manage"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "permission_required", message = "Недостаточно прав для изменения камер точки." });
        }

        var site = (await _accessStoreService.GetCompanySitesAsync(context.CompanyId, cancellationToken))
            .FirstOrDefault(item => string.Equals(item.SiteKey, siteKey, StringComparison.OrdinalIgnoreCase));
        if (site is null)
        {
            return NotFound(new { code = "site_not_found", message = "Точка не найдена." });
        }

        var deleteResult = await DeleteCameraFromServerAsync(site, cameraKey, cancellationToken);
        if (deleteResult is not null)
        {
            return deleteResult;
        }

        await _accessStoreService.DeleteCompanyCameraAsync(context.CompanyId, siteKey, cameraKey, cancellationToken);
        await _registryService.RefreshAsync(cancellationToken);
        return NoContent();
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

    private async Task<(IActionResult? Result, CompanySiteBinding? Site)> ValidateCompanyCameraMutationAsync(
        AuthenticatedCompanyContext context,
        string siteKey,
        CameraConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        if (!_accessContextService.HasPermission("zones.manage"))
        {
            return (StatusCode(StatusCodes.Status403Forbidden, new { code = "permission_required", message = "Недостаточно прав для изменения камер точки." }), null);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return (BadRequest(new { code = "camera_name_required", message = "Нужно указать название камеры." }), null);
        }

        if (string.IsNullOrWhiteSpace(request.Host))
        {
            return (BadRequest(new { code = "camera_host_required", message = "Нужно указать IP-адрес или host камеры без логина и пароля." }), null);
        }

        if (request.Host.Contains('@') || request.Host.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase))
        {
            return (BadRequest(new { code = "camera_host_must_not_contain_credentials", message = "В настройках камеры нужно указать только IP или host, без RTSP, логина и пароля." }), null);
        }

        var site = (await _accessStoreService.GetCompanySitesAsync(context.CompanyId, cancellationToken))
            .FirstOrDefault(item => string.Equals(item.SiteKey, siteKey, StringComparison.OrdinalIgnoreCase));
        return site is null
            ? (NotFound(new { code = "site_not_found", message = "Точка не найдена." }), null)
            : (null, site);
    }

    private async Task<(IActionResult? Result, RemoteCameraState? Camera)> PushCameraToServerAsync(
        CompanySiteBinding site,
        CameraConfigurationRequest request,
        string? existingCameraKey,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(CamerasController));
        if (!string.IsNullOrWhiteSpace(site.ConnectorAccessToken))
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Connector-Token", site.ConnectorAccessToken);
        }

        var url = existingCameraKey is null
            ? $"{site.ServerBaseUrl.TrimEnd('/')}/api/cameras"
            : $"{site.ServerBaseUrl.TrimEnd('/')}/api/cameras/{Uri.EscapeDataString(existingCameraKey)}";
        using var response = existingCameraKey is null
            ? await client.PostAsJsonAsync(url, request, cancellationToken)
            : await client.PutAsJsonAsync(url, request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return (StatusCode(StatusCodes.Status502BadGateway, new
            {
                code = "server_camera_configuration_failed",
                message = "Не удалось сохранить камеру на Server точки.",
                status = (int)response.StatusCode,
            }), null);
        }

        var serverCamera = await response.Content.ReadFromJsonAsync<RemoteServerCameraDto>(cancellationToken);
        if (serverCamera is null)
        {
            return (StatusCode(StatusCodes.Status502BadGateway, new { code = "server_camera_configuration_empty_response", message = "Server точки вернул пустой ответ при сохранении камеры." }), null);
        }

        return (null, new RemoteCameraState
        {
            CompanyKey = site.CompanyKey,
            SiteKey = site.SiteKey,
            SiteName = site.SiteName,
            CameraKey = $"{site.SiteKey}:{serverCamera.Key}",
            SourceCameraKey = serverCamera.Key,
            CameraId = serverCamera.Id,
            CameraName = serverCamera.Name,
            Host = serverCamera.Host,
            HighQualityPath = serverCamera.HighQualityPath,
            LowQualityPath = serverCamera.LowQualityPath,
            ServerBaseUrl = site.ServerBaseUrl,
            ConnectorAccessToken = site.ConnectorAccessToken,
            LastSyncUtc = DateTime.UtcNow,
            IsAvailable = true,
        });
    }

    private async Task<IActionResult?> DeleteCameraFromServerAsync(CompanySiteBinding site, string cameraKey, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(CamerasController));
        if (!string.IsNullOrWhiteSpace(site.ConnectorAccessToken))
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Connector-Token", site.ConnectorAccessToken);
        }

        using var response = await client.DeleteAsync($"{site.ServerBaseUrl.TrimEnd('/')}/api/cameras/{Uri.EscapeDataString(cameraKey)}", cancellationToken);
        return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound
            ? null
            : StatusCode(StatusCodes.Status502BadGateway, new { code = "server_camera_delete_failed", message = "Не удалось удалить камеру на Server точки.", status = (int)response.StatusCode });
    }

    private static CameraConfigurationRequest ToRequest(RemoteCameraState camera) => new()
    {
        Id = camera.CameraId,
        Key = camera.SourceCameraKey,
        Name = camera.CameraName,
        Host = camera.Host,
        HighQualityPath = camera.HighQualityPath,
        LowQualityPath = camera.LowQualityPath,
    };

    private static object ToCameraResponse(RemoteCameraState camera) => new
    {
        key = camera.CameraKey,
        name = camera.CameraName,
        siteKey = camera.SiteKey,
        siteName = camera.SiteName,
        cameraId = camera.CameraId,
        sourceCameraKey = camera.SourceCameraKey,
        host = camera.Host,
        highQualityPath = camera.HighQualityPath,
        lowQualityPath = camera.LowQualityPath,
        serverBaseUrl = camera.ServerBaseUrl,
        lastSyncUtc = camera.LastSyncUtc,
        isAvailable = camera.IsAvailable,
    };
}
