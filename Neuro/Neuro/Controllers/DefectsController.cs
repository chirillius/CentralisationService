using Microsoft.AspNetCore.Mvc;
using Neuro.Services;

namespace Neuro.Controllers;

[ApiController]
[Route("api/defects")]
public sealed class DefectsController : ControllerBase
{
    private readonly DefectCatalogService _catalogService;

    public DefectsController(DefectCatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    [HttpGet]
    public IActionResult GetCatalog()
    {
        return Ok(_catalogService.GetAll());
    }
}

