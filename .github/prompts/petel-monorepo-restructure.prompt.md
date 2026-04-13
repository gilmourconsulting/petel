---
mode: ask
description: Guide for restructuring the Petel monorepo into PetelATH + PetelAssistants with shared libraries
---

# Petel Monorepo Restructure Guide

This prompt captures the agreed architecture for splitting the current `PetelApp` codebase into two applications (`PetelATH` and `PetelAssistants`) sharing common infrastructure via two class libraries (`Petel.Core` and `Petel.BlazorCore`).

## Target Solution Structure

```
Petel.sln                              (renamed from PetelFullApp.sln)
│
├── shared/
│   ├── Petel.Core/                    backend shared library
│   │   └── Petel.Core.csproj
│   └── Petel.BlazorCore/              Blazor shared library
│       └── Petel.BlazorCore.csproj
│
├── PetelATH/                          renamed from current PetelApp
│   ├── PetelATH.Api/                  renamed from PetelApp.Api
│   │   └── PetelATH.Api.csproj
│   └── PetelATH.BlazorServer/         renamed from PetelApp.BlazorServer
│       └── PetelATH.BlazorServer.csproj
│
└── PetelAssistants/                   new application
    ├── PetelAssistants.Api/
    │   └── PetelAssistants.Api.csproj
    └── PetelAssistants.BlazorServer/
        └── PetelAssistants.BlazorServer.csproj
```

## Project References

- `PetelATH.Api` → `Petel.Core`
- `PetelATH.BlazorServer` → `Petel.BlazorCore`
- `PetelAssistants.Api` → `Petel.Core`
- `PetelAssistants.BlazorServer` → `Petel.BlazorCore`
- `Petel.Core` and `Petel.BlazorCore` have no cross-project references

## The Single Required Abstraction

Before moving any code, add this interface to `Petel.Core`:

```csharp
// Petel.Core/Abstractions/IAttributeCache.cs
namespace Petel.Core.Abstractions
{
    public interface IAttributeCache
    {
        string? GetAttributeValue(string name);
    }
}
```

Then in each app's `SystemAttributeCache`, implement it:

```csharp
public class SystemAttributeCache : IAttributeCache
{
    public string? GetAttributeValue(string name) =>
        GetAttributeByName(name)?.Value;
}
```

Two constructor changes in the shared files:
- `JwtTokenService`: `SystemAttributeCache` → `IAttributeCache`
- `UserSessionService`: `SystemAttributeCache?` → `IAttributeCache?`

## Petel.Core Contents

Move from `PetelApp.Api` (updating namespaces to `Petel.Core.*`):

| Source file | Destination in Petel.Core |
|---|---|
| `PetelApp.Api/Configuration/SecuritySettings.cs` | `Security/SecuritySettings.cs` |
| `PetelApp.Api/Services/DataEncryptionService.cs` | `Security/DataEncryptionService.cs` |
| `PetelApp.Api/Session/UserSession.cs` | `Session/UserSession.cs` |
| `PetelApp.Api/Session/UserSessionService.cs` | `Session/UserSessionService.cs` |
| `PetelApp.Api/Services/JwtTokenService.cs` | `Session/JwtTokenService.cs` |
| `PetelApp.Api/Controllers/BaseController.cs` | `Controllers/BaseController.cs` |
| *(new)* | `Abstractions/IAttributeCache.cs` |

`SecuritySettings` defaults: change `OtpIssuer` default value away from `"Petel External Students System"` to an empty string so each app sets its own.

## Petel.BlazorCore Contents

Move from `PetelApp.BlazorServer` (updating namespaces to `Petel.BlazorCore.*`):

| Source file | Destination in Petel.BlazorCore |
|---|---|
| `Services/TokenService.cs` | `Services/TokenService.cs` |
| `Services/AuthenticationService.cs` | `Services/AuthenticationService.cs` |
| `Services/ApiService.cs` | `Services/ApiService.cs` |
| `Services/SessionStateService.cs` | `Services/SessionStateService.cs` |
| `Services/SessionTimeoutService.cs` | `Services/SessionTimeoutService.cs` |
| `Models/ApiSettings.cs` | `Models/ApiSettings.cs` |
| `Models/SessionData.cs` | `Models/SessionData.cs` |
| Document proxy block in `Program.cs` | `Extensions/DocumentProxyExtensions.cs` |

### Document Proxy Extension Method

Extract the inline `app.MapGet("/api/documents/{documentId}/proxy", ...)` block from `PetelATH.BlazorServer/Program.cs` into:

```csharp
// Petel.BlazorCore/Extensions/DocumentProxyExtensions.cs
namespace Petel.BlazorCore.Extensions
{
    public static class DocumentProxyExtensions
    {
        public static void MapDocumentProxy(
            this WebApplication app,
            string pattern = "/api/documents/{documentId}/proxy")
        {
            app.MapGet(pattern, async (long documentId, HttpContext httpContext,
                IHttpClientFactory httpClientFactory,
                IOptions<ApiSettings> apiSettings,
                ILogger<Program> logger) =>
            {
                // ... existing proxy logic unchanged
            }).DisableAntiforgery();
        }
    }
}
```

Usage in each Blazor `Program.cs`:
```csharp
app.MapDocumentProxy();
// or with custom route:
app.MapDocumentProxy("/api/files/{documentId}/proxy");
```

## What Stays Per-App (Never Shared)

- `AppDbContext` — each app has its own schema and entity set
- All entity models — app-specific domain
- `SystemAttributeCache` — loads from app's own DB schema, implements `IAttributeCache`
- `AuthService` — queries each app's own users/passwords table
- `DocumentsController` — tied to each app's document entity model
- `ActionAuthorizationService` — each app's role/permission model
- `GlobalSystemFunctions` — domain-specific lookups per app
- All EF Core migrations
- All `appsettings.json` files

## Namespace Rename Required for PetelATH

Global find-and-replace across all `.cs` and `.razor` files:

| Old | New |
|---|---|
| `namespace PetelApp.Api` | `namespace PetelATH.Api` |
| `using PetelApp.Api` | `using PetelATH.Api` |
| `namespace PetelApp.BlazorServer` | `namespace PetelATH.BlazorServer` |
| `using PetelApp.BlazorServer` | `using PetelATH.BlazorServer` |

Note: `appsettings.json`, Azure App Service names, and deployment scripts do **not** need changes — they contain no C# namespaces.

## Namespace Mapping Summary

| Old | New |
|---|---|
| `PetelApp.Api.*` | `PetelATH.Api.*` |
| `PetelApp.BlazorServer.*` | `PetelATH.BlazorServer.*` |
| *(new shared backend)* | `Petel.Core.*` |
| *(new shared Blazor)* | `Petel.BlazorCore.*` |
| *(new app backend)* | `PetelAssistants.Api.*` |
| *(new app Blazor)* | `PetelAssistants.BlazorServer.*` |

## Order of Work

1. **Rename PetelApp → PetelATH**
   - Rename folders: `PetelApp.Api` → `PetelATH/PetelATH.Api`, `PetelApp.BlazorServer` → `PetelATH/PetelATH.BlazorServer`
   - Rename `.csproj` files
   - Update `.sln` file paths
   - Global find-and-replace namespaces
   - Build and verify

2. **Create Petel.Core**
   - `dotnet new classlib -n Petel.Core -f net9.0 -o shared/Petel.Core`
   - `dotnet sln add shared/Petel.Core`
   - Add `IAttributeCache` interface
   - Move files listed above, update namespaces
   - Apply two constructor changes (`JwtTokenService`, `UserSessionService`)
   - Add `implements IAttributeCache` to `PetelATH.Api/SystemAttributeCache`
   - Add project reference to `PetelATH.Api`
   - Build and verify

3. **Create Petel.BlazorCore**
   - `dotnet new classlib -n Petel.BlazorCore -f net9.0 -o shared/Petel.BlazorCore`
   - `dotnet sln add shared/Petel.BlazorCore`
   - Move Blazor services and models
   - Extract document proxy extension method
   - Add project reference to `PetelATH.BlazorServer`
   - Build and verify

4. **Create PetelAssistants**
   - `dotnet new webapi -n PetelAssistants.Api -f net9.0 -o PetelAssistants/PetelAssistants.Api`
   - `dotnet new blazorserver -n PetelAssistants.BlazorServer -f net9.0 -o PetelAssistants/PetelAssistants.BlazorServer`
   - Add both to solution
   - Add references to `Petel.Core` and `Petel.BlazorCore`
   - Create `PetelAssistants.Api/SystemAttributeCache` implementing `IAttributeCache`
   - Create `PetelAssistants.Api/Data/AppDbContext` with `assistants_schema`

## Database Configuration per App

```json
// PetelATH.Api/appsettings.json
{ "Database": { "SchemaName": "petel_schema" } }

// PetelAssistants.Api/appsettings.json
{ "Database": { "SchemaName": "assistants_schema" } }
```

Both use `HasDefaultSchema(_schemaName)` in their own `AppDbContext.OnModelCreating` — schemas are fully isolated.
