using Microsoft.AspNetCore.Mvc;
using PetelApp.Api.Services;

[ApiController]
[Route("api/[controller]")]
public class SystemAttributesController : ControllerBase
{
    private readonly SystemAttributeService _service;

    public SystemAttributesController(SystemAttributeService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        await _service.EnsureLoadedAsync();
        var attributes = _service.GetAllAttributes();
        return Ok(attributes);
    }
}