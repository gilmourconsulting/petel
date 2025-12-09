using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PetelApp.Api.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PetelApp.Api.Services
{
    /// <summary>
    /// Service for action-based access control
    /// 
    /// Architecture:
    /// 1. On startup: Loads role-to-actions mapping into in-memory cache
    /// 2. On user login: Loads user's roles from database
    /// 3. On action verification: Checks if user's roles have permission for the action
    /// 
    /// Supported action types:
    /// - Menu Item: Verified by menu item name
    /// - Button: Verified by screen name + button ID
    /// - Page Action: Verified by page name + action identifier
    /// - API Endpoint: Verified by endpoint path
    /// - Report: Verified by report name
    /// </summary>
    public class ActionAuthorizationService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ActionAuthorizationService> _logger;

        /// <summary>
        /// In-memory cache: Dictionary mapping role_id -> List of action_ids
        /// Loaded on startup and updated when roles/actions change
        /// Format: { role_id: [action_id1, action_id2, ...] }
        /// </summary>
        private static Dictionary<int, HashSet<int>> _roleActionsCache = new Dictionary<int, HashSet<int>>();

        /// <summary>
        /// In-memory cache: Dictionary mapping action_name -> action details
        /// Used for quick lookups without database queries during verification
        /// </summary>
        private static Dictionary<string, SystemAction> _actionsCache = new Dictionary<string, SystemAction>();

        /// <summary>
        /// Lock for thread-safe cache updates
        /// </summary>
        private static readonly object _cacheLock = new object();

        public ActionAuthorizationService(
            IServiceScopeFactory scopeFactory,
            ILogger<ActionAuthorizationService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        /// <summary>
        /// Initialize the action authorization system on application startup
        /// Loads all roles and their associated actions into memory cache
        /// </summary>
        /// <returns></returns>
        public async Task InitializeAsync()
        {
            _logger.LogInformation("🔐 Initializing Action Authorization Service...");

            try
            {
                await LoadRoleActionsAsync();
                await LoadActionsAsync();
                _logger.LogInformation("✅ Action Authorization Service initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error initializing Action Authorization Service");
                throw;
            }
        }

        /// <summary>
        /// Load all role-to-actions mappings into memory cache
        /// This is called on startup and whenever roles/actions change
        /// </summary>
        private async Task LoadRoleActionsAsync()
        {
            try
            {
                _logger.LogInformation("📋 Loading role actions into cache...");

                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var roleActions = await context.RolesActions
                    .AsNoTracking()
                    .GroupBy(ra => ra.RoleId)
                    .Select(g => new { RoleId = g.Key, ActionIds = g.Select(ra => ra.ActionId).ToList() })
                    .ToListAsync();

                lock (_cacheLock)
                {
                    _roleActionsCache.Clear();

                    foreach (var roleAction in roleActions)
                    {
                        _roleActionsCache[roleAction.RoleId] = new HashSet<int>(roleAction.ActionIds);
                    }
                }

                _logger.LogInformation("✅ Loaded {Count} roles into cache", roleActions.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error loading role actions");
                throw;
            }
        }

        /// <summary>
        /// Load all actions into memory cache
        /// Enables fast lookup by action name without database queries
        /// </summary>
        private async Task LoadActionsAsync()
        {
            try
            {
                _logger.LogInformation("📋 Loading actions into cache...");

                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var actions = await context.Set<SystemAction>()
                    .AsNoTracking()
                    .Where(a => a.IsActive)
                    .ToListAsync();

                lock (_cacheLock)
                {
                    _actionsCache.Clear();

                    foreach (var action in actions)
                    {
                        _actionsCache[action.Name.ToLower()] = action;
                    }
                }

                _logger.LogInformation("✅ Loaded {Count} actions into cache", actions.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error loading actions");
                throw;
            }
        }

        /// <summary>
        /// Verify if a user can access a menu item
        /// 
        /// Usage example:
        /// bool canAccess = await _authService.VerifyMenuItemAccessAsync(userId, "students");
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <param name="menuItemName">The menu item name (from database reference)</param>
        /// <returns>True if user has permission, false otherwise</returns>
        public async Task<bool> VerifyMenuItemAccessAsync(int userId, string menuItemName)
        {
            try
            {
                _logger.LogDebug("🔍 Verifying menu item access: user {UserId}, menu '{MenuItem}'", userId, menuItemName);

                // Find action by menu item name
                if (!_actionsCache.TryGetValue(menuItemName.ToLower(), out var action))
                {
                    _logger.LogWarning("⚠️ Menu item action not found: {MenuItem}", menuItemName);
                    return false;
                }

                return await VerifyUserActionAccessAsync(userId, action.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error verifying menu item access for user {UserId}", userId);
                return false;
            }
        }



                /// <summary>
        /// Verify onclick action access - NEW for frontend button interception
        /// Constructs action ID from screen name and function name, then checks authorization
        /// Format: {screenname}_{functionname}
        /// </summary>
        public async Task<bool> VerifyOnclickAccessAsync(int userId, string screenName, string functionName)
        {
            try
            {
                if (string.IsNullOrEmpty(screenName) || string.IsNullOrEmpty(functionName))
                {
                    _logger.LogWarning("❌ Invalid onclick params - screenName: {ScreenName}, functionName: {FunctionName}", 
                        screenName, functionName);
                    return false;
                }
        
                // Construct action ID: screenname_functionname (lowercase)
                var actionId = $"{screenName}_{functionName}".ToLower();
        
                _logger.LogInformation("🔍 Verifying onclick access - Screen: {Screen}, Function: {Function}, ActionId: {ActionId}", 
                    screenName, functionName, actionId);
        
                // Load user's roles if not already loaded
                if (!_userRoleCache.ContainsKey(userId))
                {
                    await LoadUserRolesAsync(userId);
                }
        
                // Check if user has roles
                if (!_userRoleCache.TryGetValue(userId, out var userRoles) || userRoles.Count == 0)
                {
                    _logger.LogWarning("❌ User {UserId} has no roles assigned", userId);
                    return false;
                }
        
                // Lock for thread-safe cache access
                lock (_cacheLock)
                {
                    // Get action from cache
                    if (!_actionsCache.TryGetValue(actionId, out var action))
                    {
                        // Action not registered - log as info (no security check needed)
                        _logger.LogInformation("ℹ️ Onclick action not registered - ActionId: {ActionId} (allowed, no security required)", actionId);
                        return true; // Allow if not registered
                    }
        
                    // Action exists - check if user has permission
                    foreach (var roleId in userRoles)
                    {
                        if (_roleActionsCache.TryGetValue(roleId, out var roleActions) && 
                            roleActions.Contains(action.Id))
                        {
                            _logger.LogInformation("✅ Onclick access GRANTED - User: {UserId}, ActionId: {ActionId}, RoleId: {RoleId}", 
                                userId, actionId, roleId);
                            return true;
                        }
                    }
        
                    _logger.LogWarning("🚫 Onclick access DENIED - User: {UserId}, ActionId: {ActionId}, Roles: {Roles}", 
                        userId, string.Join(",", userRoles), actionId);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error verifying onclick access - User: {UserId}, Screen: {Screen}, Function: {Function}", 
                    userId, screenName, functionName);
                return false;
            }
        }


        
                /// <summary>
                /// Verify action access by action name (generic)
                /// Used for API calls, file uploads, and other non-button actions
                /// </summary>
                public async Task<bool> VerifyActionByNameAsync(int userId, string actionName)
                {
                    try
                    {
                        _logger.LogDebug("🔍 Verifying action by name: user {UserId}, action '{ActionName}'", userId, actionName);
        
                        // Load user's roles if not cached
                        if (!_userRoleCache.ContainsKey(userId))
                        {
                            await LoadUserRolesAsync(userId);
                        }
        
                        if (!_userRoleCache.TryGetValue(userId, out var userRoles) || userRoles.Count == 0)
                        {
                            _logger.LogWarning("❌ User {UserId} has no roles assigned", userId);
                            return false;
                        }
        
                        lock (_cacheLock)
                        {
                            if (!_actionsCache.TryGetValue(actionName.ToLower(), out var action))
                            {
                                _logger.LogInformation("ℹ️ Action not registered: {ActionName} (allowed by default)", actionName);
                                return true; // Allow if not registered
                            }
        
                            foreach (var roleId in userRoles)
                            {
                                if (_roleActionsCache.TryGetValue(roleId, out var roleActions) &&
                                    roleActions.Contains(action.Id))
                                {
                                    _logger.LogInformation("✅ Action access GRANTED - User: {UserId}, Action: {ActionName}", userId, actionName);
                                    return true;
                                }
                            }
        
                            _logger.LogWarning("🚫 Action access DENIED - User: {UserId}, Action: {ActionName}", userId, actionName);
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ Error verifying action access for user {UserId}", userId);
                        return false;
                    }
                }
        
        /// <summary>
        /// Load user's roles into cache for faster lookups
        /// </summary>
        private async Task LoadUserRolesAsync(int userId)
        {
            try
            {
                _logger.LogInformation("📋 Loading roles for user {UserId}...", userId);
        
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
                var roles = await context.Set<UserRole>()
                    .AsNoTracking()
                    .Where(ur => ur.UserId == userId && ur.IsActive)
                    .Select(ur => ur.RoleId)
                    .ToListAsync();
        
                lock (_cacheLock)
                {
                    _userRoleCache[userId] = new HashSet<int>(roles);
                }
        
                _logger.LogInformation("✅ Loaded {Count} roles for user {UserId}", roles.Count, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error loading user roles");
            }
        }
        
        // Add this cache to the class fields
        private static readonly Dictionary<int, HashSet<int>> _userRoleCache = new();

        /// <summary>
        /// Verify if a user can perform an action by action ID
        /// 
        /// Internal method used by other verification methods
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <param name="actionId">The action ID to verify</param>
        /// <returns>True if user has permission, false otherwise</returns>
        public async Task<bool> VerifyUserActionAccessAsync(int userId, int actionId)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Get user's roles
                var userRoleIds = await context.UserRoles
                    .AsNoTracking()
                    .Where(ur => ur.UserId == userId && ur.IsActive)
                    .Select(ur => ur.RoleId)
                    .ToListAsync();

                if (!userRoleIds.Any())
                {
                    _logger.LogWarning("⚠️ User {UserId} has no active roles", userId);
                    return false;
                }

                // Check if any of user's roles have the action
                lock (_cacheLock)
                {
                    foreach (var roleId in userRoleIds)
                    {
                        if (_roleActionsCache.TryGetValue(roleId, out var roleActions))
                        {
                            if (roleActions.Contains(actionId))
                            {
                                _logger.LogDebug("✅ User {UserId} has access to action {ActionId} via role {RoleId}", userId, actionId, roleId);
                                return true;
                            }
                        }
                    }
                }

                _logger.LogWarning("⚠️ User {UserId} does not have permission for action {ActionId}", userId, actionId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error verifying action access for user {UserId}", userId);
                return false;
            }
        }

        /// <summary>
        /// Get all actions available to a user based on their roles
        /// Useful for dynamic UI generation
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <returns>List of action objects user has access to</returns>
        public async Task<List<object>> GetUserActionsAsync(int userId)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var userRoleIds = await context.UserRoles
                    .AsNoTracking()
                    .Where(ur => ur.UserId == userId && ur.IsActive)
                    .Select(ur => ur.RoleId)
                    .ToListAsync();

                var userActionIds = new HashSet<int>();

                lock (_cacheLock)
                {
                    foreach (var roleId in userRoleIds)
                    {
                        if (_roleActionsCache.TryGetValue(roleId, out var roleActions))
                        {
                            foreach (var actionId in roleActions)
                            {
                                userActionIds.Add(actionId);
                            }
                        }
                    }

                    // Return action details from cache
                    var result = _actionsCache.Values
                        .Where(a => userActionIds.Contains(a.Id))
                        .Select(a => new
                        {
                            id = a.Id,
                            name = a.Name,
                            displayName = a.DisplayName,
                            reference = a.Reference
                        })
                        .Cast<object>()
                        .ToList();

                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error retrieving user actions for user {UserId}", userId);
                return new List<object>();
            }
        }

        /// <summary>
        /// Refresh cache after role or action changes
        /// Call this method when roles or role-action mappings are modified
        /// </summary>
        public async Task RefreshCacheAsync()
        {
            _logger.LogInformation("🔄 Refreshing action authorization cache...");
            await LoadRoleActionsAsync();
            await LoadActionsAsync();
            _logger.LogInformation("✅ Cache refreshed successfully");
        }

        /// <summary>
        /// Get action details by name
        /// </summary>
        /// <param name="actionName">The action name</param>
        /// <returns>SystemAction if found, null otherwise</returns>
        public SystemAction? GetActionByName(string actionName)
        {
            lock (_cacheLock)
            {
                _actionsCache.TryGetValue(actionName.ToLower(), out var action);
                return action;
            }
        }
    }
}