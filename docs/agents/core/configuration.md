# Configuration Management

> Canonical: docs/agents/core/configuration.md


## Configuration Management

### Application Configuration Pattern

**CRITICAL**: All environment-specific settings must be externalized to configuration files - **NEVER hardcoded**.

#### Backend Configuration Requirements

**1. Database Configuration**

All database settings must be in `appsettings.json` and `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=petelappdb;Username=PetelAdmin;Password=..."
  },
  "Database": {
    "SchemaName": "petel_schema"
  }
}
```

**2. Database Schema Configuration Pattern**

**CRITICAL**: Use `HasDefaultSchema()` for all entities - DO NOT hardcode schema names.

```csharp
// âœ… CORRECT - AppDbContext.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using YourApp.Api.Configuration;  // e.g. PetelATH.Api.Configuration

public class AppDbContext : DbContext
{
    private readonly string _schemaName;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        IOptions<DatabaseSettings> dbSettings) 
        : base(options)
    {
        _schemaName = dbSettings.Value.SchemaName;
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // âœ… Set default schema ONCE - applies to ALL entities
        modelBuilder.HasDefaultSchema(_schemaName);

        // âœ… Configure entities WITHOUT schema parameter
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");  // Schema from HasDefaultSchema
            entity.HasIndex(e => e.Username).IsUnique();
        });

        modelBuilder.Entity<School>(entity =>
        {
            entity.ToTable("schools");  // Schema from HasDefaultSchema
        });
    }
}
```

**3. Entity Class Pattern**

```csharp
// âœ… CORRECT - Entity class (School.cs, User.cs, etc.)
[Table("schools")]  // âœ… Table name only - NO schema parameter
public class School
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
}

// âŒ WRONG - DO NOT include schema in attribute
[Table("schools", Schema = "petel_schema")]  // NO!
```

**4. Configuration Class Pattern**

```csharp
// Configuration/DatabaseSettings.cs
namespace YourApp.Api.Configuration  // e.g. PetelATH.Api.Configuration
{
    public class DatabaseSettings
    {
        public string SchemaName { get; set; } = "your_schema";  // e.g. petel_schema or assistants_schema
    }
}
```

**5. Program.cs Registration**

```csharp
// Required using statements
using Microsoft.Extensions.Options;
using YourApp.Api.Configuration;  // e.g. PetelATH.Api.Configuration

// Register configuration
builder.Services.Configure<DatabaseSettings>(
    builder.Configuration.GetSection("Database"));

// Inject into DbContext
builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    var dbSettings = serviceProvider.GetRequiredService<IOptions<DatabaseSettings>>().Value;
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions => npgsqlOptions.MigrationsHistoryTable(
            "__EFMigrationsHistory", 
            dbSettings.SchemaName
        )
    );
});
```

**6. Required Using Statements in AppDbContext**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;      // âœ… Required for IOptions<T>
using YourApp.Api.Configuration;         // âœ… Required for DatabaseSettings
```

#### Blazor Frontend Configuration Requirements

**1. Environment Configuration Pattern**

**CRITICAL**: Blazor API URLs must be in appsettings files - **NEVER hardcoded**.

```json
// âœ… CORRECT - appsettings.Development.json
{
  "ApiSettings": {
    "BaseUrl": "http://localhost:5082/api",
    "Timeout": 30
  }
}

// appsettings.Staging.json
{
  "ApiSettings": {
    "BaseUrl": "https://petel-test-api.azurewebsites.net/api",
    "Timeout": 30
  },
  "Security": {
    "Csp": {
      "ImgSrc": ["https://api.qrserver.com"]
    }
  }
}

// appsettings.Production.json
{
  "ApiSettings": {
    "BaseUrl": "https://petel-prod-api.azurewebsites.net/api",
    "Timeout": 30
  },
  "Security": {
    "Csp": {
      "ImgSrc": ["https://api.qrserver.com"]
    }
  }
}
```

**2. Content Security Policy Configuration**

CSP is automatically configured in `Program.cs` to include the API origin:

```csharp
// Build connect-src directive to include API URL
var apiBaseUrl = apiSettings?.BaseUrl ?? "";
var apiOrigin = "";
if (!string.IsNullOrEmpty(apiBaseUrl))
{
    var uri = new Uri(apiBaseUrl);
    apiOrigin = $"{uri.Scheme}://{uri.Host}";
}
var cspConnectSrcDirective = string.IsNullOrEmpty(apiOrigin) 
    ? "connect-src 'self'" 
    : $"connect-src 'self' {apiOrigin}";
```

**3. Anti-Patterns to Avoid**

```csharp
// âŒ WRONG - Hardcoded API URL in code
var apiUrl = "http://localhost:5082/api";  // NO!

// âŒ WRONG - Hardcoded CSP without API origin
context.Response.Headers.Add("Content-Security-Policy", 
    "connect-src 'self'");  // NO! This blocks external API calls

// âœ… CORRECT - Use configuration
@inject IOptions<ApiSettings> ApiSettings

var response = await ApiService.GetAsync<DataModel>("endpoint");
```

### Configuration Checklist for New Features

When adding new features, verify:

**Backend:**
1. âœ… Database connection string in `appsettings.json`
2. âœ… Schema name in `DatabaseSettings` configuration
3. âœ… `HasDefaultSchema(_schemaName)` in `AppDbContext`
4. âœ… Entity `[Table]` attributes have NO schema parameter
5. âœ… All `entity.ToTable()` calls have NO schema parameter
6. âœ… Required using statements in `AppDbContext`
7. âœ… `IOptions<DatabaseSettings>` injected into `AppDbContext` constructor

**Blazor Frontend:**
1. âœ… API URLs configured in `appsettings.{Environment}.json`
2. âœ… NO hardcoded URLs anywhere in code
3. âœ… Environment-specific appsettings files exist
4. âœ… CSP directives include API origin for `connect-src`

### Deployment Configuration

**Per-Application Deployment Scripts**:

```powershell
# Deploy PetelATH
.\Deploy-ATH.ps1 -Environment production
.\Deploy-ATH.ps1 -Environment test
.\Deploy-ATH.ps1 -Environment production -ApiOnly
.\Deploy-ATH.ps1 -Environment production -BlazorOnly
.\Deploy-ATH.ps1 -Environment production -SkipBuild

# Deploy PetelAssistants
.\Deploy-Assistants.ps1 -Environment production
.\Deploy-Assistants.ps1 -Environment test -ApiOnly
```

**Environment-Specific Configuration** (per project):
- `appsettings.Development.json` - Local development
- `appsettings.test.json` - Test environment
- `appsettings.Production.json` - Production environment

**Deployment Process**:
1. Builds project in Release configuration
2. Creates deployment package (zip)
3. Deploys to Azure App Service
4. Configures `ASPNETCORE_ENVIRONMENT`
5. Optionally configures IP restrictions

**Azure Resources** (shared App Service Plan, `israelcentral`):
- **Test**: `petel-test-rg`, `petel-test-api`, `petel-test-blazor`
- **Staging**: `petel-staging-rg`, `petel-staging-api`, `petel-staging-blazor`
- **Production**: `petel-prod-rg`, `petel-prod-api`, `petel-prod-blazor`

> Note: PetelAssistants currently shares the same Azure App Service infrastructure as PetelATH. Dedicated resources may be provisioned later â€” update `Deploy-Assistants.ps1` `$envConfig` when that happens.

### Common Configuration Errors and Fixes

**Error: `relation "schools" does not exist`**
- **Cause**: Schema not being applied to queries
- **Fix**: Verify `HasDefaultSchema(_schemaName)` is in `OnModelCreating`
- **Fix**: Remove all hardcoded `"petel_schema"` strings from `ToTable()` calls

**Error: `IOptions<DatabaseSettings> could not be found`**
- **Cause**: Missing using statement
- **Fix**: Add `using Microsoft.Extensions.Options;` to `AppDbContext.cs`

**Error: CSP blocks API connections (`connect-src 'self'`)**
- **Cause**: Content Security Policy doesn't include API origin
- **Fix**: API URL is automatically extracted from `ApiSettings.BaseUrl` in `Program.cs`
- **Fix**: Verify CSP includes `connect-src 'self' https://api-domain.com`

**Error: API calls fail in production**
- **Cause**: Wrong API URL in Blazor configuration
- **Fix**: Verify `appsettings.Production.json` has correct `ApiSettings.BaseUrl`
- **Fix**: Check Azure App Service configuration for `ASPNETCORE_ENVIRONMENT=Production`

### Benefits of This Architecture

âœ… **Environment Portability**: Deploy to any environment without code changes
âœ… **Multi-Tenant Support**: Different schemas per tenant via configuration
âœ… **Security**: Sensitive URLs not in source control
âœ… **Maintainability**: Single source of truth for all configuration
âœ… **Flexibility**: Override via environment variables or build scripts
âœ… **Testability**: Easy to switch between test/production databases
