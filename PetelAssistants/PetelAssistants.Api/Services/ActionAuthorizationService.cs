using Microsoft.EntityFrameworkCore;
using Npgsql;
using PetelAssistants.Api.Data;
using PetelAssistants.Api.Models;

namespace PetelAssistants.Api.Services
{
    /// <summary>
    /// Singleton service for action-based access control.
    ///
    /// Cache structure:
    ///   _actionsCache      — storedName (lowercase) → SystemAction  (from shared_schema)
    ///   _actionTypeCache   — action_types.name (lowercase) → action_types.id
    ///   _actionTypeIdCache — action_types.id → action_types.name
    ///   _roleActionsCache  — (entity_id, role_id) → Set&lt;action_id&gt;  (tenant-scoped)
    ///   _userRoleCache     — user_id → Set&lt;role_id&gt;
    ///
    /// Action names are unique by construction (see BuildActionName):
    ///   menu_item  → "{actionName}"              e.g. "users"
    ///   button     → "{actionName}"              e.g. "roles_create"
    ///   page_action→ "{actionName}_page_action"  e.g. "users_page_action"
    ///   others     → "{actionName}_{typeName}"   e.g. "users_api_endpoint"
    ///
    /// Action type IDs are never hardcoded — loaded from shared_schema.action_types at startup.
    /// The only code-level mapping is EventType string → action_types.name string.
    /// </summary>
    public class ActionAuthorizationService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ActionAuthorizationService> _logger;

        private static Dictionary<string, SystemAction> _actionsCache     = new(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, int>          _actionTypeCache  = new(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<int, string>          _actionTypeIdCache = new();
        private static Dictionary<(int EntityId, int RoleId), HashSet<int>> _roleActionsCache = new();
        private static Dictionary<int, HashSet<int>>    _userRoleCache    = new();
        private static readonly object _cacheLock = new();

        /// <summary>
        /// Maps EventType values (sent by Blazor) to action_types.name in shared_schema.action_types.
        /// IDs are never used here — always resolved via _actionTypeCache at runtime.
        /// </summary>
        private static readonly Dictionary<string, string> EventTypeToActionTypeName =
            new(StringComparer.OrdinalIgnoreCase)
            {
                { "MENU_NAVIGATION",      "menu_item"       },
                { "BUTTON_CLICK",         "button"          },
                { "BUTTON_VISIBLE_CHECK", "button"          },
                { "ONCLICK_BUTTON",       "button"          },
                { "PAGE_ACCESS",          "page_action"     },
                { "API_ENDPOINT",         "api_endpoint"    },
                { "REPORT",               "report"          },
                { "SPECIFIC_ACTION",      "specific_action" },
            };

        public ActionAuthorizationService(
            IServiceScopeFactory scopeFactory,
            ILogger<ActionAuthorizationService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        // ── Startup / cache management ────────────────────────────────────────

        public async Task InitializeAsync()
        {
            _logger.LogInformation("Initializing ActionAuthorizationService...");
            try
            {
                await LoadActionTypesAsync();
                await LoadActionsAsync();
                await LoadRoleActionsAsync();
                _logger.LogInformation("ActionAuthorizationService initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing ActionAuthorizationService");
            }
        }

        private async Task LoadActionTypesAsync()
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var shared = scope.ServiceProvider.GetRequiredService<SharedDbContext>();

                var types = await shared.ActionTypes
                    .AsNoTracking()
                    .Where(t => t.IsActive)
                    .ToListAsync();

                lock (_cacheLock)
                {
                    _actionTypeCache.Clear();
                    _actionTypeIdCache.Clear();
                    foreach (var t in types)
                    {
                        _actionTypeCache[t.Name]  = t.Id;
                        _actionTypeIdCache[t.Id]  = t.Name;
                    }
                }

                _logger.LogInformation("Loaded {Count} action types: [{Types}]",
                    types.Count,
                    string.Join(", ", types.Select(t => $"{t.Name}={t.Id}")));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading action types cache");
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
                        _actionsCache[BuildCacheKey(action.Name)] = action;
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

                var allRows = await context.RolesActions
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Select(ra => new { ra.EntityId, ra.RoleId, ra.ActionId })
                    .ToListAsync();

                lock (_cacheLock)
                {
                    _roleActionsCache.Clear();
                    foreach (var row in allRows)
                    {
                        var key = (row.EntityId, row.RoleId);
                        if (!_roleActionsCache.TryGetValue(key, out var set))
                            _roleActionsCache[key] = set = new HashSet<int>();
                        set.Add(row.ActionId);
                    }
                }

                _logger.LogInformation("Loaded {Count} role-action rows into cache ({Keys} entity/role keys)",
                    allRows.Count, _roleActionsCache.Count);
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
            await LoadActionTypesAsync();
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

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Constructs the unique DB action name from request parameters.
        /// - menu_item and button: use actionName as-is (existing naming convention)
        /// - all other types: append the type suffix so the same base name can coexist
        ///   per type, e.g. "users" (menu_item) and "users_page_action" (page_action).
        /// </summary>
        private static string BuildActionName(string actionName, string typeName)
        {
            var name = actionName.ToLower().Trim();
            var type = typeName.ToLower().Trim();
            return type is "menu_item" or "button" ? name : $"{name}_{type}";
        }

        /// <summary>Cache key equals the stored DB action name (both lowercased).</summary>
        private static string BuildCacheKey(string storedName)
            => storedName.ToLower();

        // ── Core authorization ────────────────────────────────────────────────

        /// <summary>
        /// Verifies whether userId may perform actionName for the given EventType and screen.
        ///
        /// Flow:
        ///   1. Resolve action_type from EventType via EventTypeToActionTypeName + _actionTypeCache.
        ///   2. Check _actionsCache by composite key "{actionName}:{typeName}".
        ///   3. Cache miss → check shared_schema.actions by (name, action_type_id).
        ///      a. Not in DB  → create the action, log it, return DENIED.
        ///      b. In DB      → add to cache, continue to role check.
        ///   4. Cache hit (or step 3b) → check user roles → GRANTED or DENIED.
        /// </summary>
        public async Task<bool> VerifyActionByNameAsync(
            int userId,
            int entityId,
            string actionName,
            string eventType,
            string screenName,
            string? reference = null)
        {
            try
            {
                // Step 1 — resolve action type from EventType
                if (!EventTypeToActionTypeName.TryGetValue(eventType, out var typeName))
                {
                    _logger.LogWarning("Unknown EventType '{EventType}' for action '{ActionName}' — defaulting to 'button'", eventType, actionName);
                    typeName = "button";
                }

                int actionTypeId;
                lock (_cacheLock)
                    _actionTypeCache.TryGetValue(typeName, out actionTypeId);

                if (actionTypeId == 0)
                {
                    _logger.LogWarning("Action type '{TypeName}' not found in cache — cannot verify '{ActionName}'", typeName, actionName);
                    return false;
                }

                // Construct the unique DB name (e.g. "users_page_action" for PAGE_ACCESS)
                var dbActionName = BuildActionName(actionName, typeName);
                var cacheKey     = BuildCacheKey(dbActionName);

                // Step 2 — cache lookup by constructed name
                SystemAction? action;
                lock (_cacheLock)
                    _actionsCache.TryGetValue(cacheKey, out action);

                if (action != null)
                {
                    _logger.LogInformation("Action '{DbName}' (ID: {Id}) — FOUND IN CACHE", dbActionName, action.Id);
                }
                else
                {
                    _logger.LogInformation("Action '{DbName}' — NOT in cache, checking DB", dbActionName);

                    // Step 3 — DB lookup by constructed name
                    using var scope = _scopeFactory.CreateScope();
                    var shared = scope.ServiceProvider.GetRequiredService<SharedDbContext>();

                    action = await shared.SystemActions
                        .AsNoTracking()
                        .FirstOrDefaultAsync(a => a.Name == dbActionName);

                    if (action == null)
                    {
                        // Step 3a — not in DB: create, log, deny
                        _logger.LogInformation("Action '{DbName}' — NOT in DB, creating", dbActionName);
                        await CreateActionInDbAsync(shared, dbActionName, typeName, actionTypeId, screenName, reference);
                        return false;
                    }

                    // Step 3b — found in DB: add to cache and reload role grants
                    // so actions inserted after process start (e.g. SQL seed) are usable.
                    _logger.LogInformation("Action '{DbName}' (ID: {Id}) — found in DB, adding to cache", dbActionName, action.Id);
                    lock (_cacheLock)
                        _actionsCache[cacheKey] = action;
                    await LoadRoleActionsAsync();
                }

                // Step 4 — role check
                if (!_userRoleCache.ContainsKey(userId))
                    await LoadUserRolesAsync(userId);

                if (!_userRoleCache.TryGetValue(userId, out var userRoles) || userRoles.Count == 0)
                {
                    _logger.LogWarning("Access DENIED for user {UserId} (entity {EntityId}) on '{DbName}' — user has no roles",
                        userId, entityId, dbActionName);
                    return false;
                }

                lock (_cacheLock)
                {
                    foreach (var roleId in userRoles)
                    {
                        var roleKey = (entityId, roleId);
                        if (_roleActionsCache.TryGetValue(roleKey, out var roleActions) && roleActions.Contains(action.Id))
                            return true;
                    }
                }

                _logger.LogWarning("Access DENIED for user {UserId} (entity {EntityId}) on '{DbName}' (ID: {ActionId}) — UserRoles=[{Roles}]",
                    userId, entityId, dbActionName, action.Id, string.Join(",", userRoles));
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying action '{ActionName}' for user {UserId}", actionName, userId);
                return false;
            }
        }

        /// <summary>
        /// Inserts a new action into shared_schema.actions using the pre-constructed dbActionName.
        /// Caller always returns DENIED after this — the action needs a role assignment first.
        /// </summary>
        private async Task CreateActionInDbAsync(
            SharedDbContext shared,
            string dbActionName,
            string typeName,
            int actionTypeId,
            string screenName,
            string? reference)
        {
            try
            {
                var parts       = dbActionName.Split('_', 2);
                var displayName = parts.Length > 1 ? parts[1] : dbActionName;

                var newAction = new SystemAction
                {
                    Name         = dbActionName,
                    DisplayName  = displayName,
                    Reference    = reference ?? screenName,
                    Description  = $"Auto-created: name='{dbActionName}' type='{typeName}' screen='{screenName}'",
                    ActionTypeId = actionTypeId,
                    IsActive     = true,
                    CreatedAt    = DateTime.UtcNow,
                    UpdatedAt    = DateTime.UtcNow
                };

                shared.SystemActions.Add(newAction);

                try
                {
                    await shared.SaveChangesAsync();
                    _logger.LogInformation(
                        "Created action '{DbName}' (ID: {Id}, type='{TypeName}', TypeId: {TypeId}, screen='{Screen}') in shared_schema.actions",
                        newAction.Name, newAction.Id, typeName, actionTypeId, screenName);

                    lock (_cacheLock)
                        _actionsCache[BuildCacheKey(newAction.Name)] = newAction;
                }
                catch (DbUpdateException dbEx) when (dbEx.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
                {
                    // Race condition — another request created the same name first
                    _logger.LogWarning("Race condition creating '{DbName}' — fetching winner from DB", dbActionName);
                    var winner = await shared.SystemActions.FirstOrDefaultAsync(a => a.Name == dbActionName);
                    if (winner != null)
                        lock (_cacheLock) { _actionsCache[BuildCacheKey(winner.Name)] = winner; }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating action '{DbName}' type='{TypeName}'", dbActionName, typeName);
            }
        }

        // ── Helpers used by other callers ────────────────────────────────────

        public async Task<bool> VerifyUserActionAccessAsync(int userId, int entityId, int actionId)
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
                        var key = (entityId, roleId);
                        if (_roleActionsCache.TryGetValue(key, out var roleActions) && roleActions.Contains(actionId))
                            return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying action {ActionId} for user {UserId} (entity {EntityId})", actionId, userId, entityId);
                return false;
            }
        }

        public List<int> GetUserAllowedActionIds(int userId, int entityId)
        {
            lock (_cacheLock)
            {
                if (!_userRoleCache.TryGetValue(userId, out var userRoles) || userRoles.Count == 0)
                    return new List<int>();

                var result = new HashSet<int>();
                foreach (var roleId in userRoles)
                {
                    var key = (entityId, roleId);
                    if (_roleActionsCache.TryGetValue(key, out var roleActions))
                        result.UnionWith(roleActions);
                }
                return result.ToList();
            }
        }

        public SystemAction? GetActionByName(string actionName)
        {
            lock (_cacheLock)
            {
                _actionsCache.TryGetValue(BuildCacheKey(actionName), out var action);
                return action;
            }
        }
    }
}
