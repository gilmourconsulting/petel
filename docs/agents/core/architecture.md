# Architecture & Local Development

> Canonical: docs/agents/core/architecture.md. App details: docs/agents/apps/


## Architecture Overview

**Petel** is an educational management platform built as a **.NET 9 monorepo** with two applications sharing two class libraries.

### Solution Structure (`Petel.sln`)

```
PetelATH/
  PetelATH.Api/            <- Production ATH backend (Web API, net9.0)
  PetelATH.BlazorServer/   <- Production ATH frontend (Blazor Server, net9.0)
PetelAssistants/
  PetelAssistants.Api/     <- Assistants backend (Web API, net9.0)
  PetelAssistants.BlazorServer/ <- Assistants frontend (Blazor Server, net9.0)
shared/
  Petel.Core/              <- Shared backend library (auth, session, JWT, encryption)
  Petel.BlazorCore/        <- Shared Blazor library (services, models, proxy extension)
```

**App-specific instructions:**
- [docs/agents/apps/petel-ath.md](../apps/petel-ath.md) — PetelATH
- [docs/agents/apps/petel-assistants.md](../apps/petel-assistants.md) — PetelAssistants

### Shared Libraries

#### `Petel.Core` (namespace: `Petel.Core.*`)
All API projects reference this. Contains:
- `Abstractions/IAttributeCache.cs` â€” `string? GetAttributeValue(string name)` interface
- `Security/SecuritySettings.cs` â€” JWT + OTP configuration POCO
- `Security/DataEncryptionService.cs` â€” AES encryption service
- `Session/UserSession.cs` â€” in-memory session model
- `Session/UserSessionService.cs` â€” session store; constructor takes `IAttributeCache? attributeCache = null`
- `Session/JwtTokenService.cs` â€” JWT generation/validation; constructor takes `IAttributeCache attributeCache`
- `Controllers/BaseController.cs` â€” base class with `GetCurrentSession()`, `GetSessionProperty()`

Each API project must provide its own `SystemAttributeCache : IAttributeCache` implementation and register it in DI.

#### `Petel.BlazorCore` (namespace: `Petel.BlazorCore.*`)
All Blazor projects reference this. Contains:
- `Services/` â€” `TokenService`, `AuthenticationService`, `ApiService`, `SessionStateService`, `SessionTimeoutService`
- `Models/ApiSettings.cs`, `Models/SessionData.cs`
- `Extensions/DocumentProxyExtensions.cs` â€” `app.MapDocumentProxy()` minimal API endpoint

## Critical Development Workflows

### Local Development Setup
```bash
# PetelATH API (port 5082)
cd PetelATH/PetelATH.Api && dotnet run
# OR: double-click "Start Local Api.cmd"

# PetelATH Blazor frontend
cd PetelATH/PetelATH.BlazorServer && dotnet run
# OR: double-click "Start Blazor Server.cmd"

# PetelAssistants API
cd PetelAssistants/PetelAssistants.Api && dotnet run

# PetelAssistants Blazor frontend
cd PetelAssistants/PetelAssistants.BlazorServer && dotnet run
```

ATH API runs on `http://localhost:5082`, ATH Blazor on `https://localhost:5001` / `http://localhost:5000`
