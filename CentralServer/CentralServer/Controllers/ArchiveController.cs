using Microsoft.AspNetCore.Mvc;
using CentralServer.Services;

namespace CentralServer.Controllers;

[ApiController]
[Route("api/archive")]
public sealed class ArchiveController : ControllerBase
{
    private readonly MotionFrameIndexService _indexService;
    private readonly IWebHostEnvironment _environment;
    private readonly CompanyAccessContextService _accessContextService;

    public ArchiveController(MotionFrameIndexService indexService, IWebHostEnvironment environment, CompanyAccessContextService accessContextService)
    {
        _indexService = indexService;
        _environment = environment;
        _accessContextService = accessContextService;
    }

    [HttpGet("frames")]
    public IActionResult GetRecentFrames([FromQuery] string? cameraKey, [FromQuery] int take = 30)
    {
        var normalizedTake = Math.Clamp(take, 1, 100);
        var company = _accessContextService.RequireCurrent();
        return Ok(_indexService.GetRecent(cameraKey, normalizedTake)
            .Where(record => record.SiteKey.StartsWith($"{company.CompanyKey}-", StringComparison.OrdinalIgnoreCase)));
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

        var fullPath = Path.Combine(_environment.ContentRootPath, "videos", record.RelativePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
        if (!System.IO.File.Exists(fullPath))
        {
            return NotFound(new { message = "Файл кадра движения отсутствует на диске." });
        }

        return PhysicalFile(fullPath, "image/jpeg");
    }
}
