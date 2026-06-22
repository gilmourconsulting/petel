using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PetelAssistants.Api.Data;

namespace PetelAssistants.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SystemAttributesController : ControllerBase
    {
        private readonly SharedDbContext _sharedContext;
        private readonly ILogger<SystemAttributesController> _logger;

        public SystemAttributesController(SharedDbContext sharedContext, ILogger<SystemAttributesController> logger)
        {
            _sharedContext = sharedContext;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var attrs = await _sharedContext.SystemAttributes
                    .AsNoTracking()
                    .OrderBy(a => a.Id)
                    .Select(a => new
                    {
                        id = a.Id,
                        name = a.Name,
                        value = a.Value,
                        valueType = a.ValueType,
                        description = a.Description
                    })
                    .ToListAsync();

                return Ok(attrs);
            }
            catch (PostgresException ex) when (ex.SqlState == "42P01")
            {
                _logger.LogWarning(ex, "system_attributes table not found; returning empty configuration list");
                return Ok(Array.Empty<object>());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error loading system attributes; returning empty configuration list");
                return Ok(Array.Empty<object>());
            }
        }
    }
}
