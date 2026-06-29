using Microsoft.EntityFrameworkCore;
using PetelAssistants.Api.Data;
using PetelAssistants.Api.Models;

namespace PetelAssistants.Api.Services
{
    /// <summary>
    /// Singleton service for action-based access control.
    ///
    /// Cache structure:
    ///   _actionsCache      — action_name (lowercase) → SystemAction  (global, from shared_schema)
    ///   _roleActionsCache  — role_id → Set&lt;action_id&gt;               (all tenants, IgnoreQueryFilters)
    ///   _userRoleCache     — user_id → Set&lt;role_id&gt;                  (all tenants, IgnoreQueryFilters)
    ///
    /// IDs are globally unique SERIAL values so tenant isolation is maintained by ID uniqueness.
    /// </summary>
    public class ActionAuthorizationService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ActionAuthorizationService> _logger;

        private static Dictionary<string, SystemAction> _actionsCache = new();
        private static Dictionary<int, HashSet<int>>    _roleActionsCache = new();
        private static Dictionary<int, HashSet<int>>    _userRoleCache = new();
        private static readonly object _cacheLock = new();

        public ActionAuthorizationService(
            IServiceScopeFactory scopeFactory,
            ILogger<ActionAuthorizationService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            _logger.LogInformation("Initializing ActionAuthorizationService...");
            try
            {
                await LoadActionsAsync();
                await LoadRoleActionsAsync();
                _logger.LogInformation("ActionAuthorizationService initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing ActionAuthorizationService");
            }
        }

        private async Task LoadActionsAsync()
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var shared = scope.ServiceProvider.GetRequiredService<SharedDbContext>();

                var actions = await shared.SystemActions
                    .AsNoTracking()
                    .Where(a => a.IsActive)
                    .ToListAsync();

                lock (_cacheLock)
                {
                    _actionsCache.Clear();
                    foreach (var action in actions)
                        _actionsCache[action.Name.ToLower()] = action;
                }

                _logger.LogInformation("Loaded {Count} actions into cache", actions.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading actions cache");
            }
        }

        private async Task LoadRoleActionsAsync()
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AssistDbContext>();

                var roleActions = await context.RolesActions
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .GroupBy(ra => ra.RoleId)
                    .Select(g => new { RoleId = g.Key, ActionIds = g.Select(ra => ra.ActionId).ToList() })
                    .ToListAsync();

                lock (_cacheLock)
                {
                    _roleActionsCache.Clear();
                    foreach (var ra in roleActions)
                        _roleActionsCache[ra.RoleId] = new HashSet<int>(ra.ActionIds);
                }

                _logger.LogInformation("Loaded {Count} roles into role-actions cache", roleActions.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading role-actions cache");
            }
        }

        private async Task LoadUserRolesAsync(int userId)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AssistDbContext>();

                var roles = await context.UserRoles
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(ur => ur.UserId == userId && ur.IsActive)
                    .Select(ur => ur.RoleId)
                    .ToListAsync();

                lock (_cacheLock)
                {
                    _userRoleCache[userId] = new HashSet<int>(roles);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading roles for user {UserId}", userId);
            }
        }

        public async Task RefreshCacheAsync()
        {
            _logger.LogInformation("Refreshing ActionAuthorizationService cache...");
            lock (_cacheLock)
            {
                _userRoleCache.Clear();
            }
            await LoadActionsAsync();
            await LoadRoleActionsAsync();
            _logger.LogInformation("Cache refreshed successfully");
        }

        public void InvalidateUserCache(int userId)
        {
            lock (_cacheLock)
            {
                _userRoleCache.Remove(userId);
            }
        }

        public async Task<bool> VerifyMenuItemAccessAsync(int userId, string menuItemName)
        {
            try
            {
                SystemAction? action;
                lock (_cacheLock)
                {
                    _actionsCache.TryGetValue(menuItemName.ToLower(), out action);
                }
                if (action == null)
                {
                    _logger.LogWarning("Menu item action not found: {MenuItem}", menuItemName);
                    return false;
                }
                return await VerifyUserActionAccessAsync(userId, action.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying menu item access for user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> VerifyOnclickAccessAsync(int userId, string screenName, string functionName)
        {
            var whitelisted = new[] { "close", "toggle", "cancel", "refresh", "navigate" };
            if (whitelisted.Any(p => functionName.ToLower().StartsWith(p)))
                return true;

            var actionId = $"{screenName}_{functionName}".ToLower();
            return await VerifyActionByNameAsync(userId, actionId);
        }

        public async Task<bool> VerifyActionByNameAsync(int userId, string actionName, int actionType = 7, string? reference = null)
        {
            try
            {
                if (!_userRoleCache.ContainsKey(userId))
                    await LoadUserRolesAsync(userId);

                if (!_userRoleCache.TryGetValue(userId, out var userRoles) || userRoles.Count == 0)
                    return false;

                SystemAction? action;
                lock (_cacheLock)
                {
                    _actionsCache.TryGetValue(actionName.ToLower(), out action);
                }

                if (action == null)
                {
                    _logger.LogWarning("Action not found in cache: {ActionName} — auto-creating as active", actionName);
                    action = await AutoCreateActionAsync(actionName, actionType, reference);
                    if (action == null) return false;
                }

                lock (_cacheLock)
                {
                    foreach (var roleId in userRoles)
                    {
                        if (_roleActionsCache.TryGetValue(roleId, out var roleActions) && roleActions.Contains(action.Id))
                            return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying action {ActionName} for user {UserId}", actionName, userId);
                return false;
            }
        }

        private async Task<SystemAction?> AutoCreateActionAsync(string actionName, int actionTypeId, string? reference)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var shared = scope.ServiceProvider.GetRequiredService<SharedDbContext>();

                var existing = await shared.SystemActions.FirstOrDefaultAsync(a => a.Name == actionName);
                if (existing != null)
                {
                    lock (_cacheLock) { _actionsCache[actionName.ToLower()] = existing; }
                    return existing;
                }

                var parts = actionName.Split('_', 2);
                var displayName = parts.Length > 1 ? parts[1] : actionName;

                var newAction = new SystemAction
                {
                    Name          = actionName,
                    DisplayName   = displayName,
                    Reference     = reference ?? (parts.Length > 0 ? parts[0] : actionName),
                    Description   = $"Auto-created from action name '{actionName}'",
                    ActionTypeId  = actionTypeId,
                    IsActive      = true,
                    CreatedAt     = DateTime.UtcNow,
                    UpdatedAt     = DateTime.UtcNow
                };

                shared.SystemActions.Add(newAction);
                await shared.SaveChangesAsync();

                lock (_cacheLock) { _actionsCache[newAction.Name.ToLower()] = newAction; }
                return newAction;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error auto-creating action {ActionName}", actionName);
                return null;
            }
        }

        public async Task<bool> VerifyUserActionAccessAsync(int userId, int actionId)
        {
            try
            {
                if (!_userRoleCache.ContainsKey(userId))
                    await LoadUserRolesAsync(userId);

                if (!_userRoleCache.TryGetValue(userId, out var userRoles) || userRoles.Count == 0)
                    return false;

                lock (_cacheLock)
                {
                    foreach (var roleId in userRoles)
                    {
                        if (_roleActionsCache.TryGetValue(roleId, out var roleActions) && roleActions.Contains(actionId))
                            return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying action {ActionId} for user {UserId}", actionId, userId);
                return false;
            }
        }

        public List<int> GetUserAllowedActionIds(int userId)
        {
            lock (_cacheLock)
            {
                if (!_userRoleCache.TryGetValue(userId, out var userRoles) || userRoles.Count == 0)
                    return new List<int>();

                var result = new HashSet<int>();
                foreach (var roleId in userRoles)
                {
                    if (_roleActionsCache.TryGetValue(roleId, out var roleActions))
                        result.UnionWith(roleActions);
                }
                return result.ToList();
            }
        }

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
