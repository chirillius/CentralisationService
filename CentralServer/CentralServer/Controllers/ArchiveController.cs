using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using CentralServer.Models;
using CentralServer.Services;

namespace CentralServer.Controllers;

[ApiController]
[Route("api/archive")]
public sealed class ArchiveController : ControllerBase
{
    private readonly MotionFrameIndexService _indexService;
    private readonly CompanyAccessContextService _accessContextService;
    private readonly CentralArchivePathService _pathService;
    private readonly MotionMonitoringOptions _options;

    public ArchiveController(
        MotionFrameIndexService indexService,
        CompanyAccessContextService accessContextService,
        CentralArchivePathService pathService,
        IOptions<MotionMonitoringOptions> options)
    {
        _indexService = indexService;
        _accessContextService = accessContextService;
        _pathService = pathService;
        _options = options.Value;
    }

    [HttpGet("frames")]
    public IActionResult GetRecentFrames([FromQuery] string? cameraKey, [FromQuery] int take = 30)
    {
        var normalizedTake = Math.Clamp(take, 1, 100);
        var company = _accessContextService.RequireCurrent();
        return Ok(_indexService.GetRecent(cameraKey, normalizedTake)
            .Where(record => string.Equals(record.CompanyKey, company.CompanyKey, StringComparison.OrdinalIgnoreCase)));
    }

    [HttpGet("frame/{**relativePath}")]
    public IActionResult GetFrameFile(string relativePath)
    {
        var record = _indexService.FindByRelativePath(relativePath);
        if (record is null)
        {
            return NotFound(new { message = "Кадр движения не найден." });
        }
        if (!_accessContextService.CanAccessSite(record.SiteKey))
        {
            return NotFound(new { message = "Кадр движения не найден." });
        }

        var fullPath = _pathService.BuildFullPathFromRelative(_options.VideosRootPath, record.RelativePath);
        if (!System.IO.File.Exists(fullPath))
        {
            return NotFound(new { message = "Файл кадра движения отсутствует на диске." });
        }

        return PhysicalFile(fullPath, ResolveContentType(fullPath));
    }

    private static string ResolveContentType(string fullPath)
    {
        return Path.GetExtension(fullPath).ToLowerInvariant() switch
        {
            ".mp4" => "video/mp4",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".json" => "application/json",
            _ => "application/octet-stream",
        };
    }
}
