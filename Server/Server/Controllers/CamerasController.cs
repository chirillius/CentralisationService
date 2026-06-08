using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Server.Models;
using Server.Services;

namespace Server.Controllers;

[ApiController]
[Route("api/cameras")]
public sealed class CamerasController : ControllerBase
{
    private readonly CameraConfigurationService _cameraConfigurationService;
    private readonly FfmpegFrameGrabber _frameGrabber;
    private readonly ConnectorBindingService _bindingService;

    public CamerasController(
        CameraConfigurationService cameraConfigurationService,
        FfmpegFrameGrabber frameGrabber,
        ConnectorBindingService bindingService)
    {
        _cameraConfigurationService = cameraConfigurationService;
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

        return Ok(_cameraConfigurationService.GetCameras().Select(camera => new
        {
            Id = camera.Id,
            Key = camera.ResolveKey(),
            Name = camera.Name,
            Host = camera.ResolveHost(),
            camera.HighQualityPath,
            camera.LowQualityPath,
        }));
    }

    [HttpPost]
    public async Task<IActionResult> AddCamera([FromBody] CameraConfigurationRequest request, CancellationToken cancellationToken)
    {
        if (!await _bindingService.IsRequestAuthorizedAsync(Request, cancellationToken))
        {
            return Unauthorized(new { code = "connector_token_required", message = "Нужен токен доступа коннектора." });
        }

        try
        {
            var camera = await _cameraConfigurationService.UpsertAsync(request, existingKey: null, cancellationToken);
            return Ok(ToCameraResponse(camera));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "camera_configuration_invalid", message = ex.Message });
        }
    }

    [HttpPut("{cameraKey}")]
    public async Task<IActionResult> UpdateCamera(string cameraKey, [FromBody] CameraConfigurationRequest request, CancellationToken cancellationToken)
    {
        if (!await _bindingService.IsRequestAuthorizedAsync(Request, cancellationToken))
        {
            return Unauthorized(new { code = "connector_token_required", message = "Нужен токен доступа коннектора." });
        }

        try
        {
            var camera = await _cameraConfigurationService.UpsertAsync(request, cameraKey, cancellationToken);
            return Ok(ToCameraResponse(camera));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "camera_configuration_invalid", message = ex.Message });
        }
    }

    [HttpDelete("{cameraKey}")]
    public async Task<IActionResult> DeleteCamera(string cameraKey, CancellationToken cancellationToken)
    {
        if (!await _bindingService.IsRequestAuthorizedAsync(Request, cancellationToken))
        {
            return Unauthorized(new { code = "connector_token_required", message = "Нужен токен доступа коннектора." });
        }

        return await _cameraConfigurationService.DeleteAsync(cameraKey, cancellationToken)
            ? NoContent()
            : NotFound(new { message = $"Камера '{cameraKey}' не настроена." });
    }

    [HttpGet("{cameraKey}/frame")]
    public async Task<IActionResult> GetFrame(string cameraKey, CancellationToken cancellationToken)
    {
        if (!await _bindingService.IsRequestAuthorizedAsync(Request, cancellationToken))
        {
            return Unauthorized(new { code = "connector_token_required", message = "Нужен токен доступа коннектора." });
        }

        var camera = _cameraConfigurationService.GetCameras().FirstOrDefault(item =>
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

    private static object ToCameraResponse(CameraSource camera)
    {
        return new
        {
            camera.Id,
            Key = camera.ResolveKey(),
            camera.Name,
            Host = camera.ResolveHost(),
            camera.HighQualityPath,
            camera.LowQualityPath,
        };
    }
}
