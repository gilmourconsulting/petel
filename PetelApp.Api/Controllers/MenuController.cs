using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;
using PetelApp.Api.Session;

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MenuController : BaseController
    {
        private readonly AppDbContext _context;

        public MenuController(
            AppDbContext context,
            UserSessionService userSessionService,
            ILogger<MenuController> logger)
            : base(userSessionService, logger)
        {
            _context = context;
        }

        /// <summary>
        /// Get menu items for current user based on permissions
        /// Returns all active menu items with null action_id or action_id user has privileges for
        /// Following Authentication & Session Management pattern
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetMenuItems()
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    _logger.LogWarning("No valid session found for menu items request");
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                _logger.LogInformation(
                    "Loading menu items for user: {Username} (ID: {UserId})", 
                    session.Username, 
                    session.UserId);

                // TODO: When implementing security, filter by user privileges
                // For now, return all active items with null action_id
                var menuItems = await _context.MenuItems
                    .AsNoTracking()
                    .Where(m => m.IsActive && m.ActionId == null)
                    .OrderBy(m => m.SortOrder)
                    .Select(m => new
                    {
                        id = m.Id,
                        name = m.Name,
                        reference = m.Reference,
                        text = m.Text,
                        sortOrder = m.SortOrder
                    })
                    .ToListAsync();

                _logger.LogInformation(
                    "Loaded {Count} menu items for user {Username}", 
                    menuItems.Count, 
                    session.Username);

                return Ok(menuItems);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading menu items");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת תפריט",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Admin endpoint to get all menu items (for future menu management)
        /// Following BaseController pattern
        /// </summary>
        [HttpGet("all")]
        public async Task<IActionResult> GetAllMenuItems()
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var menuItems = await _context.MenuItems
                    .AsNoTracking()
                    .OrderBy(m => m.SortOrder)
                    .ToListAsync();

                _logger.LogInformation("Loaded all {Count} menu items", menuItems.Count);

                return Ok(menuItems);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading all menu items");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת תפריט",
                    error = ex.Message
                });
            }
        }
    }
}