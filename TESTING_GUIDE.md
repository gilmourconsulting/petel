# Blazor Server Testing Guide

## Quick Start - 3 Simple Steps

### Step 1: Start the Backend API
**Location**: Root folder (`c:\dev\PetelFullApp`)
**Command**: Double-click `Start Local Api.cmd`
**Expected Output**: 
```
Now listening on: http://localhost:5082
Application started. Press Ctrl+C to shut down.
```
**Leave this window open** - the API must keep running

---

### Step 2: Start the Blazor Server
**Location**: Root folder (`c:\dev\PetelFullApp`)
**Command**: Double-click `Start Blazor Server.cmd`
**Expected Output**:
```
Now listening on: http://localhost:5001
Application started. Press Ctrl+C to shut down.
```
**Leave this window open** - the Blazor app must keep running

---

### Step 3: Open Your Browser
**URL**: http://localhost:5001
**Expected**: Login page should appear

---

## Alternative: Manual Commands

If batch files don't work, use PowerShell:

### Terminal 1 - Start API
```powershell
cd c:\dev\PetelFullApp\PetelApp.Api
dotnet run
```

### Terminal 2 - Start Blazor (in a NEW terminal)
```powershell
cd c:\dev\PetelFullApp\PetelApp.BlazorServer
dotnet run
```

### Browser
```
http://localhost:5001
```

---

## Testing Checklist

### ✅ 1. Login Page
- [ ] Page loads without errors (check browser console F12)
- [ ] Entity dropdown populates with entities from database
- [ ] Enter username/password
- [ ] Login button works

**Test Credentials** (use your existing database users):
- Username: [your test username]
- Password: [your test password]
- Entity: [select from dropdown]

### ✅ 2. Main Dashboard
After login, you should see:
- [ ] Top bar with logo, system info, user info
- [ ] Side menu loads from database (Menu items)
- [ ] Year navigation buttons (Previous/Current/Next)
- [ ] Alerts card
- [ ] Events card
- [ ] All content in Hebrew (RTL)

### ✅ 3. Navigation
Click menu items and verify:
- [ ] About page loads
- [ ] Settings page loads
- [ ] Analytics page loads
- [ ] Swagger page loads (embedded iframe)

### ✅ 4. Form Pages
Navigate to form pages and test:
- [ ] EntityDetails - Click "ערוך פרטים" (Edit), change data, save
- [ ] SchoolYearConfig - View years, click edit/add buttons
- [ ] Settings - Try changing password

### ✅ 5. Session Management
Test session handling:
- [ ] User info displays correctly in top bar
- [ ] Menu items load based on your permissions
- [ ] Logout button works
- [ ] After logout, redirects to login page

---

## Troubleshooting

### Problem: API won't start
**Error**: `Address already in use`
**Solution**: Another process is using port 5082
```powershell
# Find and kill the process
netstat -ano | findstr :5082
taskkill /PID <process_id> /F
```

### Problem: Blazor won't start
**Error**: `Address already in use`
**Solution**: Another process is using port 5001
```powershell
# Find and kill the process
netstat -ano | findstr :5001
taskkill /PID <process_id> /F
```

### Problem: Login page loads but entity dropdown is empty
**Check**:
1. API is running (http://localhost:5082/swagger should load)
2. Browser console (F12) for errors
3. Database connection in `PetelApp.Api/appsettings.json`

### Problem: "Unauthorized" errors
**Check**:
1. JWT token service is working (check API logs)
2. Session is created on login (check browser console)
3. Authorization header is sent with requests (F12 > Network tab)

### Problem: Menu doesn't load
**Check**:
1. Database has records in `petel_schema.menu_items` table
2. API endpoint `/api/menu` returns data (test in Swagger)
3. Browser console for errors

### Problem: Pages show "Loading..." forever
**Check**:
1. Browser console (F12) for JavaScript errors
2. Network tab (F12) - are API calls failing?
3. API logs - are endpoints being hit?

### Problem: Hebrew text displays incorrectly
**Check**:
1. Browser encoding is UTF-8
2. CSS files loaded correctly (check Network tab)
3. `dir="rtl"` is set on HTML element

---

## Browser Developer Tools

Press **F12** to open developer tools:

### Console Tab
- Shows JavaScript errors
- Shows API call logs
- Shows navigation events

### Network Tab
- Shows all HTTP requests
- Click on API calls to see request/response
- Red items = failed requests
- Check "Preserve log" to see all requests

### Application Tab
- Session Storage - should see `authToken` after login
- Cookies - check for authentication cookies

---

## Expected API Endpoints (Test in Swagger)

Open http://localhost:5082/swagger and test:

### Authentication
- `POST /api/auth/login` - Login with username/password
- `GET /api/session` - Get current session info

### Menu
- `GET /api/menu` - Should return menu items from database

### Data Endpoints
- `GET /api/alerts` - Alerts for dashboard
- `GET /api/events` - Events for dashboard
- `GET /api/entities` - List of entities
- `GET /api/schoolyears` - List of school years

---

## Success Criteria

✅ **Phase 1 is working correctly if**:
1. Login page loads and authentication works
2. Main dashboard displays with menu from database
3. All 11 migrated pages load without errors
4. Forms can be edited and saved
5. Navigation between pages works
6. Logout redirects to login
7. Session is maintained across page navigation
8. All UI is in Hebrew with proper RTL layout

---

## Next Steps After Testing

Once basic testing is complete:

**Report any issues found**:
- Which page/feature failed?
- What was the error message?
- Browser console errors (F12)
- Network errors (F12 > Network tab)

**If everything works**:
- Ready to proceed to Phase 2 (DataGrid component and table pages)
- Consider implementing action-based security
- Plan file upload/download features

---

## Quick Reference

| What | Where | Command |
|------|-------|---------|
| Start API | Root folder | Double-click `Start Local Api.cmd` |
| Start Blazor | Root folder | Double-click `Start Blazor Server.cmd` |
| View App | Browser | http://localhost:5001 |
| View Swagger | Browser | http://localhost:5082/swagger |
| Stop Services | Terminal windows | Press `Ctrl+C` |

---

**Need Help?**
- Check browser console (F12)
- Check API terminal for errors
- Check Blazor terminal for errors
- Review error messages carefully
