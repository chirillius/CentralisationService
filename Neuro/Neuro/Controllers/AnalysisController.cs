using CentralisationService.Entities.Models.Vision;
using Microsoft.AspNetCore.Mvc;
using Neuro.Services;

namespace Neuro.Controllers;

[ApiController]
[Route("api/analysis")]
public sealed class AnalysisController : ControllerBase
{
    private readonly RetailAnalysisService _retailAnalysisService;

    public AnalysisController(RetailAnalysisService retailAnalysisService)
    {
        _retailAnalysisService = retailAnalysisService;
    }

    [HttpPost("{defectKey}")]
    public IActionResult Analyze(string defectKey)
    {
        return Ok(new
        {
            defectKey,
            handledBy = "CentralisationService.Neuro",
            status = "not-implemented-yet",
            note = "The centralized AI service endpoint is reserved and defect catalog is already aligned with the existing Tobacco domain."
        });
    }

    [HttpPost("retail-scene")]
    public ActionResult<RetailSceneAnalysisResponse> AnalyzeRetailScene([FromBody] RetailSceneAnalysisRequest request)
    {
        if (request.FrameJpegBytes.Length == 0)
        {
            return BadRequest(new { message = "frameJpegBytes is required." });
        }

        var response = _retailAnalysisService.Analyze(request);
        return Ok(response);
    }
}
