using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelATH.Api.Data;
using PetelATH.Api.Services;
using PetelATH.Api.Session;

namespace PetelATH.Api.Controllers
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
    
                    // Get all active menu items
                    var allMenuItems = await _context.MenuItems
                        .AsNoTracking()
                        .Where(m => m.IsActive)
                        .OrderBy(m => m.SortOrder)
                        .ToListAsync();
    
                    _logger.LogInformation("Found {TotalCount} active menu items", allMenuItems.Count);
    
                    // Filter menu items based on user permissions
                    var filteredMenuItems = new List<object>();
    
                    foreach (var menuItem in allMenuItems)
                    {

    

                        // Use the menu item NAME as the action identifier
                        var hasPermission = await _actionAuthService.VerifyMenuItemAccessAsync(userId, menuItem.Name);
    
                        if (hasPermission)
                        {
                            _logger.LogDebug("Menu item '{Name}' authorized for user {UserId}", menuItem.Name, userId);
                            
                            filteredMenuItems.Add(new
                            {
                                id = menuItem.Id,
                                name = menuItem.Name,
                                reference = menuItem.Reference,
                                text = menuItem.Text,
                                sortOrder = menuItem.SortOrder
                            });
                        }
                        else
                        {
                            _logger.LogDebug(
                                "Menu item '{Name}' filtered out - user {UserId} lacks permission", 
                                menuItem.Name,
                                userId);
                        }
                    }
    
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