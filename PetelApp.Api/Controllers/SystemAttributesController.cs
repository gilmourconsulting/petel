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
    public IActionResult GetAll()
    {
        var attributes = _service.GetAllAttributes();
        return Ok(attributes);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        await _service.LoadAttributesAsync();
        return Ok(new { success = true });
    }
}