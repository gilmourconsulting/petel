using PetelAssistants.BlazorServer.Components;
using PetelAssistants.BlazorServer.Services;
using Petel.BlazorCore.Models;
using Petel.BlazorCore.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Blazor Server ──────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ── API settings ───────────────────────────────────────────────────────────
builder.Services.Configure<ApiSettings>(
    builder.Configuration.GetSection("ApiSettings"));
builder.Services.Configure<Petel.BlazorCore.Models.ApiSettings>(
    builder.Configuration.GetSection("ApiSettings"));

var apiSettings = builder.Configuration.GetSection("ApiSettings").Get<ApiSettings>();
builder.Services.AddHttpClient("PetelApi", client =>
{
    client.BaseAddress = new Uri(apiSettings?.BaseUrl ?? "http://localhost:5238/api");
    client.Timeout = TimeSpan.FromSeconds(apiSettings?.Timeout ?? 30);
})
.SetHandlerLifetime(TimeSpan.FromMinutes(10));

// ── Scoped services ────────────────────────────────────────────────────────
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<AuthenticationService>();
builder.Services.AddScoped<ApiService>();
builder.Services.AddScoped<SessionStateService>();
builder.Services.AddScoped<SessionTimeoutService>();
builder.Services.AddScoped<ActionSecurityService>();

// ── Blazor Server circuit options ──────────────────────────────────────────
builder.Services.AddServerSideBlazor()
    .AddCircuitOptions(options =>
    {
        options.DetailedErrors = builder.Environment.IsDevelopment();
        options.DisconnectedCircuitMaxRetained = 100;
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(3);
        options.JSInteropDefaultCallTimeout = TimeSpan.FromMinutes(1);
        options.MaxBufferedUnacknowledgedRenderBatches = 10;
    });

// ── URLs (Azure App Service / non-Development) ─────────────────────────────
if (!builder.Environment.IsDevelopment())
{
    var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

var app = builder.Build();

// ── CSP headers (non-Development only) ────────────────────────────────────
var apiBaseUrl = apiSettings?.BaseUrl ?? "";
var apiOrigin = string.Empty;
if (!string.IsNullOrEmpty(apiBaseUrl))
{
    try
    {
        var uri = new Uri(apiBaseUrl);
        apiOrigin = $"{uri.Scheme}://{uri.Host}";
    }
    catch { }
}
var cspConnectSrc = string.IsNullOrEmpty(apiOrigin)
    ? "connect-src 'self'"
    : $"connect-src 'self' {apiOrigin}";

// ── Pipeline ───────────────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseRouting();

if (!app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        context.Response.Headers.Append("Permissions-Policy", "geolocation=(), microphone=(), camera=()");
        context.Response.Headers.Append("Content-Security-Policy",
            $"default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; {cspConnectSrc}; frame-ancestors 'none';");
        await next();
    });
}

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
