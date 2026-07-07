using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Petel.Core.Abstractions;
using Petel.Core.Security;
using Petel.Core.Session;
using PetelAssistants.Api.Data;
using PetelAssistants.Api.Services;
using PetelAssistants.Api.Tenancy;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();

// ── CORS ───────────────────────────────────────────────────────────────────
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5239", "https://localhost:7239" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials());
});

// ── Configuration ──────────────────────────────────────────────────────────
builder.Services.Configure<DatabaseSettings>(
    builder.Configuration.GetSection("Database"));
builder.Services.Configure<SharedDatabaseSettings>(
    builder.Configuration.GetSection("SharedDatabase"));
builder.Services.Configure<SecuritySettings>(
    builder.Configuration.GetSection("Security"));

// ── Tenant context (scoped per request) ───────────────────────────────────
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();

// ── Data encryption (required before AssistDbContext) ─────────────────────
builder.Services.AddSingleton<DataEncryptionService>();

// ── assist_schema DbContext (tenant-scoped, global query filters active) ──
builder.Services.AddDbContext<AssistDbContext>((serviceProvider, options) =>
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

// ── shared_schema DbContext (global reference data, no tenant filter) ─────
builder.Services.AddDbContext<SharedDbContext>((serviceProvider, options) =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    );
});

// ── Session / auth services ────────────────────────────────────────────────
builder.Services.AddSingleton<SystemAttributeCache>();
builder.Services.AddSingleton<IAttributeCache>(sp => sp.GetRequiredService<SystemAttributeCache>());
builder.Services.AddSingleton<UserSessionService>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddHostedService<SystemAttributeLoaderHostedService>();

// ── Action authorization service ───────────────────────────────────────────
builder.Services.AddSingleton<ActionAuthorizationService>();

// ── Domain services ────────────────────────────────────────────────────────
builder.Services.AddScoped<PersonService>();
builder.Services.AddScoped<OrgUnitService>();
builder.Services.AddScoped<EntitlementService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var jwtService = scope.ServiceProvider.GetRequiredService<JwtTokenService>();
    var sessionService = scope.ServiceProvider.GetRequiredService<UserSessionService>();
    sessionService.SetJwtTokenService(jwtService);
}

// ── Initialize action authorization cache ─────────────────────────────────
try
{
    var actionAuthService = app.Services.GetRequiredService<ActionAuthorizationService>();
    await actionAuthService.InitializeAsync();
}
catch (Exception ex)
{
    var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
    startupLogger.LogWarning(ex, "ActionAuthorizationService initialization failed — tables may not exist yet");
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowBlazorClient");

app.MapControllers();

app.Run();
