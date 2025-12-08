using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;
using PetelApp.Api.Services;
using PetelApp.Api.Session;

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MenuController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly ActionAuthorizationService _actionAuthService;

        public MenuController(
            AppDbContext context,
            ActionAuthorizationService actionAuthService,
            UserSessionService userSessionService,
            ILogger<MenuController> logger)
            : base(userSessionService, logger)
        {
            _context = context;
            _actionAuthService = actionAuthService;
        }

        /// <summary>
        /// Get menu items for current user based on permissions
        /// NOW: Filters menu items based on user's role-based action permissions
        /// Following Authentication & Session Management + Action-Based Security patterns
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

                var userId = int.Parse(session.UserId);

                // Get all user's permitted actions (from security cache)
                var userActions = await _actionAuthService.GetUserActionsAsync(userId);
                var permittedActionNames = userActions
                    .Select(a => ((dynamic)a).name.ToString().ToLower())
                    .ToHashSet();

                _logger.LogInformation(
                    "User {Username} has {ActionCount} permitted actions", 
                    session.Username, 
                    permittedActionNames.Count);

                // Get all active menu items
                var allMenuItems = await _context.MenuItems
                    .AsNoTracking()
                    .Where(m => m.IsActive)
                    .OrderBy(m => m.SortOrder)
                    .ToListAsync();

                // Filter menu items based on user permissions
                var filteredMenuItems = allMenuItems
                    .Where(m => {
                        // Menu items with null action_id are always visible (no security check)
                        if (m.ActionId == null)
                        {
                            _logger.LogDebug("Menu item '{Name}' has no action restriction (visible to all)", m.Name);
                            return true;
                        }

                        // Check if user has permission for this menu item's action
                        // Menu item name should match action name in actions table
                        var hasPermission = permittedActionNames.Contains(m.Name.ToLower());
                        
                        if (!hasPermission)
                        {
                            _logger.LogDebug(
                                "Menu item '{Name}' filtered out - user lacks permission", 
                                m.Name);
                        }
                        
                        return hasPermission;
                    })
                    .Select(m => new
                    {
                        id = m.Id,
                        name = m.Name,
                        reference = m.Reference,
                        text = m.Text,
                        sortOrder = m.SortOrder
                    })
                    .ToList();

                _logger.LogInformation(
                    "Loaded {FilteredCount} menu items (filtered from {TotalCount} total) for user {Username}", 
                    filteredMenuItems.Count, 
                    allMenuItems.Count,
                    session.Username);

                return Ok(filteredMenuItems);
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