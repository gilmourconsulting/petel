[ApiController]
[Route("api/[controller]")]
public class EntitiesController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetEntities()
    {
        // Query your entities table
        // Return list of entities
        
        return Ok(entities);
    }
}