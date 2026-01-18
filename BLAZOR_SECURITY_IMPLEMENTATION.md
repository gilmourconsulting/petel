# Blazor Security Implementation

**Date**: January 18, 2026  
**Status**: Complete - Authentication and Session Timeout Implemented

---

## Overview

The Blazor Server application now has full security features matching the original vanilla JS implementation:

1. ✅ **Authentication Guards** - Unauthorized users redirected to login
2. ✅ **Session Timeout** - Auto-logout after idle period
3. ✅ **Activity Tracking** - User interactions reset idle timer
4. ✅ **Session Clearing** - Login page clears previous sessions

---

## Architecture

### Services

#### AuthenticationService
**Location**: `Services/AuthenticationService.cs`

**Purpose**: Manages authentication state and redirects

**Key Methods**:
```csharp
Task<bool> IsAuthenticatedAsync()        // Check if user has valid token
Task<bool> EnsureAuthenticatedAsync()    // Check auth, redirect if not
Task LogoutAsync()                        // Clear token and redirect to login
```

**Registration**: `Program.cs` - `builder.Services.AddScoped<AuthenticationService>()`

#### SessionTimeoutService
**Location**: `Services/SessionTimeoutService.cs`

**Purpose**: Tracks user idle time and triggers auto-logout

**Configuration**:
- Default timeout: **10 minutes**
- Warning shown: **2 minutes before timeout**
- Loads timeout config from backend: `GET /api/session/timeout-config`

**Key Methods**:
```csharp
Task InitializeAsync()          // Start timeout tracking
void OnUserActivity()           // Reset idle timer on user interaction
void ContinueSession()          // User clicked "Continue" on warning
Task LogoutNowAsync()           // User clicked "Logout" on warning
void Stop()                     // Stop timeout tracking
```

**Events**:
```csharp
event Action? OnShowWarning     // Warning modal should be shown
event Action? OnHideWarning     // Warning modal should be hidden
event Action? OnAutoLogout      // Auto-logout triggered
```

**Registration**: `Program.cs` - `builder.Services.AddScoped<SessionTimeoutService>()`

---

## Components

### AuthenticationGuard
**Location**: `Components/Security/AuthenticationGuard.razor`

**Purpose**: Wraps protected content, ensures user is authenticated

**Usage**:
```razor
@layout MainLayout

<AuthenticationGuard>
    <p>Protected content here</p>
</AuthenticationGuard>
```

**Behavior**:
- Checks authentication on component initialization
- Shows loading message while checking
- Redirects to `/login` if not authenticated
- Renders child content if authenticated

**Implementation in MainLayout**:
```razor
@inject AuthenticationService AuthService
@inject SessionTimeoutService TimeoutService

<AuthenticationGuard>
    <SessionTimeoutWarning />
    
    <!-- All layout content -->
    <div class="app-container">
        <!-- Top bar, menu, content, footer -->
    </div>
</AuthenticationGuard>
```

### SessionTimeoutWarning
**Location**: `Components/Shared/SessionTimeoutWarning.razor`

**Purpose**: Shows warning modal before auto-logout

**Features**:
- Modal overlay with RTL Hebrew text
- Displays remaining time (2 minutes)
- "Continue" button - resets idle timer
- "Logout Now" button - immediately logs out
- Listens to `SessionTimeoutService` events
- Initializes JavaScript activity tracking

**JavaScript Integration**:
```javascript
// wwwroot/js/sessionTimeout.js
window.SessionTimeout.initialize(dotNetReference)
window.SessionTimeout.onUserActivity() // Calls back to Blazor
```

**Activity Events Tracked**:
- `mousedown`
- `keydown`
- `scroll`
- `touchstart`
- `click`

---

## How It Works

### 1. Page Load (Authenticated Route)

```
User navigates to /maindashboard
    ↓
MainLayout renders
    ↓
<AuthenticationGuard> checks token
    ↓
    ├─ Token exists → Render content
    │                 Initialize SessionTimeoutService
    │                 Start idle timer (10 min)
    │                 Track user activity
    │
    └─ No token → Redirect to /login
```

### 2. User Activity Flow

```
User clicks/types/scrolls
    ↓
JavaScript detects event
    ↓
Calls dotNetRef.invokeMethodAsync('OnUserActivity')
    ↓
SessionTimeoutWarning.OnUserActivity()
    ↓
TimeoutService.OnUserActivity()
    ↓
Reset idle timer (restart 10 min countdown)
Hide warning modal (if shown)
```

### 3. Idle Timeout Flow

```
User inactive for 8 minutes
    ↓
Warning timer triggers
    ↓
TimeoutService.OnShowWarning event fires
    ↓
SessionTimeoutWarning shows modal
    "Your session will expire in 2 minutes"
    ↓
    ├─ User clicks "Continue"
    │  → TimeoutService.ContinueSession()
    │  → Reset idle timer
    │  → Hide modal
    │
    ├─ User clicks "Logout Now"
    │  → TimeoutService.LogoutNowAsync()
    │  → AuthService.LogoutAsync()
    │  → Clear token
    │  → Redirect to /login
    │
    └─ User inactive for 2 more minutes (10 min total)
       → Auto-logout timer triggers
       → TimeoutService.PerformAutoLogoutAsync()
       → AuthService.LogoutAsync()
       → Clear token
       → Redirect to /login
```

### 4. Login Flow

```
User navigates to /login
    ↓
Login.razor OnAfterRenderAsync(firstRender = true)
    ↓
Clear any existing token: TokenService.RemoveTokenAsync()
Clear session cache: SessionState.ClearSession()
    ↓
User selects entity and enters credentials
    ↓
POST /api/auth/login → Receive JWT token
    ↓
Store token: TokenService.SaveTokenAsync(token)
    ↓
Navigate to /maindashboard
    ↓
AuthenticationGuard allows access
SessionTimeoutService starts tracking
```

---

## Configuration

### Timeout Settings

**Backend Configuration** (recommended):
```csharp
// API endpoint: GET /api/session/timeout-config
{
    "TimeoutMinutes": 10
}
```

**Service Default**:
```csharp
private int _idleTimeoutMinutes = 10; // Default: 10 minutes
private int _warningTimeMinutes = 2;  // Show warning 2 minutes before
```

### Adjusting Timeout

**Option 1**: Backend API endpoint
- Implement `GET /api/session/timeout-config` endpoint
- Return `{ "TimeoutMinutes": 15 }` for 15-minute timeout
- Service loads config on initialization

**Option 2**: Modify service default
```csharp
// SessionTimeoutService.cs
private int _idleTimeoutMinutes = 15; // Change from 10 to 15
```

---

## Testing

### Test Authentication Guard

1. Open browser without logging in
2. Navigate to `http://localhost:5000/maindashboard`
3. **Expected**: Immediate redirect to `/login`

### Test Session Timeout

1. Login to application
2. Do NOT interact with page for 8 minutes
3. **Expected**: Warning modal appears: "Your session will expire in 2 minutes"
4. Click "Continue"
5. **Expected**: Modal disappears, timer resets

### Test Auto-Logout

1. Login to application
2. Do NOT interact with page for 10 minutes
3. **Expected**: Auto-logout, redirect to `/login`

### Test Activity Tracking

1. Login to application
2. After 5 minutes, click somewhere on page
3. Wait another 6 minutes (11 minutes total from login)
4. **Expected**: Still logged in (timer was reset by click at 5 min)

### Test Login Session Clearing

1. Login to application
2. Navigate to `/login` manually (URL bar)
3. **Expected**: Previous session cleared, must login again

---

## Differences from Vanilla JS Implementation

### ✅ Equivalent Features

| Feature | Vanilla JS | Blazor |
|---------|-----------|--------|
| Authentication check | `checkAuthentication()` | `AuthenticationService.IsAuthenticatedAsync()` |
| Auto-redirect to login | `window.location.href = 'login.html'` | `NavigationManager.NavigateTo("/login", forceLoad: true)` |
| Session timeout tracking | `session-timeout.js` | `SessionTimeoutService` |
| Activity detection | DOM event listeners | JavaScript interop + C# service |
| Warning modal | HTML/JS modal | Blazor component |
| Token storage | `sessionStorage.setItem('authToken')` | `ProtectedSessionStorage` (encrypted) |

### ⭐ Improvements in Blazor

1. **Type Safety** - C# type checking vs dynamic JavaScript
2. **Encrypted Storage** - `ProtectedSessionStorage` vs plain `sessionStorage`
3. **Component Lifecycle** - Automatic cleanup via `IDisposable`
4. **Dependency Injection** - Services injected, not global variables
5. **Single Guard Component** - One `<AuthenticationGuard>` protects all pages
6. **Event-Driven** - Clean event model vs callback functions

---

## Common Issues & Solutions

### Issue: "User not authenticated" loop

**Cause**: Token not being saved properly after login

**Solution**: Check `TokenService.SaveTokenAsync()` is called after login:
```csharp
await TokenService.SaveTokenAsync(response.Token);
```

### Issue: Timeout not working

**Cause**: JavaScript file not loaded

**Solution**: Verify `sessionTimeout.js` is referenced in `App.razor`:
```html
<script src="/js/sessionTimeout.js"></script>
```

### Issue: Activity tracking not resetting timer

**Cause**: JavaScript interop not initialized

**Solution**: Check `SessionTimeoutWarning.OnAfterRenderAsync` calls:
```csharp
await JSRuntime.InvokeVoidAsync("SessionTimeout.initialize", _dotNetRef);
```

### Issue: Warning modal not showing

**Cause**: Events not wired up

**Solution**: Verify `SessionTimeoutWarning.OnInitialized`:
```csharp
TimeoutService.OnShowWarning += HandleShowWarning;
TimeoutService.OnHideWarning += HandleHideWarning;
```

---

## Files Changed

### New Files Created

```
PetelApp.BlazorServer/
├── Services/
│   ├── AuthenticationService.cs          ✨ New
│   └── SessionTimeoutService.cs          ✨ New
├── Components/
│   ├── Security/
│   │   └── AuthenticationGuard.razor     ✨ New
│   └── Shared/
│       └── SessionTimeoutWarning.razor   ✨ New
└── wwwroot/
    └── js/
        └── sessionTimeout.js             ✨ New
```

### Modified Files

```
PetelApp.BlazorServer/
├── Program.cs                            🔧 Added service registrations
├── Components/
│   ├── App.razor                        🔧 Added sessionTimeout.js reference
│   ├── Layout/
│   │   └── MainLayout.razor             🔧 Wrapped in <AuthenticationGuard>
│   └── Pages/
│       └── Login.razor                  🔧 Clear session on page load
```

---

## Future Enhancements

### Planned

- [ ] Backend API endpoint for timeout configuration
- [ ] User preference for timeout duration
- [ ] Remember device (extended session)
- [ ] Session persistence across browser tabs

### Possible

- [ ] Real-time notifications via SignalR
- [ ] Concurrent session limits
- [ ] IP-based access restrictions
- [ ] Audit log for security events

---

**Last Updated**: January 18, 2026  
**Status**: Security implementation complete and tested ✅
