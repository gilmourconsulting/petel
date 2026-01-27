# Security Cache Refresh Solution

## Problem
When new actions are added to the database (actions table), they still show as "unknown reference" because the ActionAuthorizationService uses in-memory caches that are only loaded on application startup.

## Root Cause
The ActionAuthorizationService caches actions and role-action mappings in memory for performance:
- `_actionsCache`: Maps action names to action details
- `_roleActionsCache`: Maps role IDs to lists of action IDs  
- `_userRoleCache`: Maps user IDs to their role IDs

These caches are loaded once on startup via `InitializeAsync()` and are not automatically refreshed when database changes occur.

## Solution Implemented

### 1. Added Refresh Cache Button
- Added "רענן מטמון אבטחה" (Refresh Security Cache) button to the Roles management page
- Button calls the existing `POST /api/roles/refresh-cache` endpoint
- Provides immediate feedback to users when cache is refreshed

### 2. Database Action Registration
- Created SQL script to add `roles_refreshcache` action to actions table
- Assigns the action to Administrator role
- Ensures the button itself is properly secured

### 3. User Workflow
1. Administrator adds new actions to database (via RoleDetails page or database scripts)
2. Administrator clicks "רענן מטמון אבטחה" button on Roles page  
3. System reloads all caches from database
4. New actions are immediately available for authorization checks

## Alternative Solutions (Not Implemented)

### Automatic Cache Invalidation
Could implement automatic cache refresh when actions are modified, but this adds complexity and potential performance overhead.

### TTL-Based Cache
Could implement time-to-live based cache expiration, but immediate refresh gives better user control.

### Database Change Notifications
Could use PostgreSQL LISTEN/NOTIFY or change tracking, but adds infrastructure complexity.

## Files Modified

1. **PetelApp.BlazorServer/Components/Pages/Roles.razor**
   - Added RefreshSecurityCache button
   - Added RefreshSecurityCache method

2. **SQL/add_refresh_cache_action.sql** (New)
   - Adds the refresh cache action to database
   - Assigns action to Administrator role

## Usage Instructions

1. Run the SQL script to add the refresh cache action:
   ```sql
   \i SQL/add_refresh_cache_action.sql
   ```

2. When new actions are added to the database:
   - Go to Roles management page (/roles)
   - Click "רענן מטמון אבטחה" button  
   - System will reload all security caches
   - New actions will be immediately available

## Benefits

✅ **Immediate Solution**: No application restart required  
✅ **User Control**: Administrators can refresh cache when needed  
✅ **Secure**: Cache refresh action is itself secured via role-based permissions  
✅ **Simple**: Uses existing infrastructure, no new complexity  
✅ **Feedback**: Users get confirmation when cache is refreshed