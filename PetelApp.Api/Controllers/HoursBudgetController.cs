using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;
using PetelApp.Api.Models;
using PetelApp.Api.Session;
using Microsoft.Extensions.Logging;

namespace PetelApp.Api.Controllers
{
    /// <summary>
    /// Hours budget management following multi-tenant request flow
    /// Inherits from BaseController for tenant isolation
    /// CURRENTLY DISABLED - Not in use as of September 10, 2025
    /// </summary>
    [ApiController]
    [ApiExplorerSettings(IgnoreApi = true)] // Disable API routing to this controller
    [Route("api/[controller]")]
    public class HoursBudgetController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly ILogger<HoursBudgetController> _logger;

        // Fix constructor - BaseController doesn't take parameters
        public HoursBudgetController(AppDbContext context, ILogger<HoursBudgetController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Controller methods are preserved but disabled from API routing
        // ...existing methods...
    }

    // Request DTOs following project-specific patterns
    // ...existing DTOs...
}
