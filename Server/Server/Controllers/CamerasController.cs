using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Server.Models;
using Server.Services;

namespace Server.Controllers;

[ApiController]
[Route("api/cameras")]
public sealed class CamerasController : ControllerBase
{
    private readonly ServerNodeOptions _options;
    private readonly FfmpegFrameGrabber _frameGrabber;
    private readonly ConnectorBindingService _bindingService;

    public CamerasController(IOptions<ServerNodeOptions> options, FfmpegFrameGrabber frameGrabber, ConnectorBindingService bindingService)
    {
        _options = options.Value;
        _frameGrabber = frameGrabber;
        _bindingService = bindingService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<RegisteredCameraInfo>>> GetCameras(CancellationToken cancellationToken)
    {
        if (!await _bindingService.IsRequestAuthorizedAsync(Request, cancellationToken))
        {
            return Unauthorized(new { code = "connector_token_required", message = "Нужен токен доступа коннектора." });
        }

        return Ok(_options.Cameras.Select(camera => new RegisteredCameraInfo
        {
            Id = camera.Id,
            Key = camera.ResolveKey(),
            Name = camera.Name,
        }));
    }

    [HttpGet("{cameraKey}/frame")]
    public async Task<IActionResult> GetFrame(string cameraKey, CancellationToken cancellationToken)
    {
        if (!await _bindingService.IsRequestAuthorizedAsync(Request, cancellationToken))
        {
            return Unauthorized(new { code = "connector_token_required", message = "Нужен токен доступа коннектора." });
        }

        var camera = _options.Cameras.FirstOrDefault(item =>
            string.Equals(item.ResolveKey(), cameraKey, StringComparison.OrdinalIgnoreCase));

        if (camera is null)
        {
            return NotFound(new { message = $"Камера '{cameraKey}' не настроена." });
        }

        try
        {
            var frame = await _frameGrabber.CaptureFrameAsync(camera, cancellationToken);
            return File(frame, "image/jpeg");
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                camera = camera.Name,
                message = ex.Message,
            });
        }
    }
}
