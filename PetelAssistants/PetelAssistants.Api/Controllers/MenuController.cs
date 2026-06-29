using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Petel.Core.Controllers;
using Petel.Core.Session;
using PetelAssistants.Api.Data;

namespace PetelAssistants.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MenuController : BaseController
    {
        private readonly SharedDbContext _sharedContext;

        public MenuController(
            SharedDbContext sharedContext,
            UserSessionService userSessionService,
            ILogger<MenuController> logger)
            : base(userSessionService, logger)
        {
            _sharedContext = sharedContext;
        }

        /// <summary>Returns active menu items for the authenticated user.</summary>
        [HttpGet]
        public async Task<IActionResult> GetMenuItems()
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            try
            {
                var menuItems = await _sharedContext.MenuItems
                    .AsNoTracking()
                    .Where(m => m.IsActive)
                    .OrderBy(m => m.SortOrder)
                    .Select(m => new
                    {
                        id        = m.Id,
                        name      = m.Name,
                        reference = m.Reference,
                        text      = m.Text,
                        sortOrder = m.SortOrder
                    })
                    .ToListAsync();

                return Ok(menuItems);
            }
            catch (PostgresException ex) when (ex.SqlState == "42P01")
            {
                _logger.LogWarning(ex, "menu_items table not found; returning empty list");
                return Ok(Array.Empty<object>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading menu items");
                return Ok(Array.Empty<object>());
            }
        }
    }
}
