using Microsoft.AspNetCore.Mvc;

namespace CentralServer.Controllers;

[ApiController]
[Route("api/system")]
public sealed class SystemController : ControllerBase
{
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            service = "CentralisationService.CentralServer",
            processingMode = "centralized",
            utcNow = DateTime.UtcNow,
        });
    }
}
