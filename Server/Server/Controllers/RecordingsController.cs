using Microsoft.AspNetCore.Mvc;
using Server.Models;
using Server.Services;

namespace Server.Controllers;

[ApiController]
[Route("api/cameras/{cameraKey}/recordings")]
public sealed class RecordingsController : ControllerBase
{
    private readonly CameraRecordingService _recordingService;
    private readonly ConnectorBindingService _bindingService;

    public RecordingsController(CameraRecordingService recordingService, ConnectorBindingService bindingService)
    {
        _recordingService = recordingService;
        _bindingService = bindingService;
    }

    [HttpPost("start")]
    public async Task<IActionResult> Start(string cameraKey, [FromBody] StartCameraRecordingRequest request, CancellationToken cancellationToken)
    {
        if (!await _bindingService.IsRequestAuthorizedAsync(Request, cancellationToken))
        {
            return Unauthorized(new { code = "connector_token_required", message = "Нужен токен доступа коннектора." });
        }

        try
        {
            return Ok(await _recordingService.StartAsync(cameraKey, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "recording_start_failed", message = ex.Message });
        }
    }

    [HttpPost("stop")]
    public async Task<IActionResult> Stop(string cameraKey, CancellationToken cancellationToken)
    {
        if (!await _bindingService.IsRequestAuthorizedAsync(Request, cancellationToken))
        {
            return Unauthorized(new { code = "connector_token_required", message = "Нужен токен доступа коннектора." });
        }

        var recording = await _recordingService.StopAsync(cameraKey, cancellationToken);
        return recording is null
            ? NotFound(new { code = "recording_not_running", message = "Активная запись камеры не найдена." })
            : Ok(recording);
    }

    [HttpGet("{recordingId}")]
    public async Task<IActionResult> Get(string cameraKey, string recordingId, CancellationToken cancellationToken)
    {
        if (!await _bindingService.IsRequestAuthorizedAsync(Request, cancellationToken))
        {
            return Unauthorized(new { code = "connector_token_required", message = "Нужен токен доступа коннектора." });
        }

        var recording = _recordingService.GetRecording(recordingId);
        return recording is null
            ? NotFound(new { code = "recording_not_found", message = "Запись не найдена." })
            : Ok(recording);
    }

    [HttpGet("{recordingId}/download")]
    public async Task<IActionResult> Download(string cameraKey, string recordingId, CancellationToken cancellationToken)
    {
        if (!await _bindingService.IsRequestAuthorizedAsync(Request, cancellationToken))
        {
            return Unauthorized(new { code = "connector_token_required", message = "Нужен токен доступа коннектора." });
        }

        var filePath = _recordingService.GetRecordingFilePath(recordingId);
        if (string.IsNullOrWhiteSpace(filePath) || !System.IO.File.Exists(filePath))
        {
            return NotFound(new { code = "recording_file_not_found", message = "Файл записи ещё не готов или отсутствует." });
        }

        return PhysicalFile(filePath, "video/mp4", Path.GetFileName(filePath));
    }
}
