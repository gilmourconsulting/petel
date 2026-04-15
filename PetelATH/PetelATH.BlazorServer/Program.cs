using Microsoft.Extensions.Options;
using Petel.BlazorCore.Extensions;
using PetelATH.BlazorServer.Components;
using PetelATH.BlazorServer.Models;
using PetelATH.BlazorServer.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure URLs - Only for Azure App Service Linux (non-Development)
// In Development, launchSettings.json will be used instead
if (!builder.Environment.IsDevelopment())
{
    var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure API settings
builder.Services.Configure<ApiSettings>(
    builder.Configuration.GetSection("ApiSettings"));

// ✅ FIX: Configure HttpClient as named client with proper lifetime
var apiSettings = builder.Configuration.GetSection("ApiSettings").Get<ApiSettings>();
builder.Services.AddHttpClient("PetelApi", client =>
{
    client.BaseAddress = new Uri(apiSettings?.BaseUrl ?? "http://localhost:5082");
    client.Timeout = TimeSpan.FromSeconds(apiSettings?.Timeout ?? 30);
})
.SetHandlerLifetime(TimeSpan.FromMinutes(10)); // Prevent frequent recreation

builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<ApiService>();
builder.Services.AddScoped<SessionStateService>();
builder.Services.AddScoped<AuthenticationService>();
builder.Services.AddScoped<SessionTimeoutService>();
builder.Services.AddScoped<ActionSecurityService>();

// Configure Blazor Server options
builder.Services.AddServerSideBlazor()
    .AddCircuitOptions(options =>
    {
        options.DetailedErrors = builder.Environment.IsDevelopment();
        options.DisconnectedCircuitMaxRetained = 100;
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(3);
        options.JSInteropDefaultCallTimeout = TimeSpan.FromMinutes(1);
        // ✅ FIX: Increase max buffer size for SignalR
        options.MaxBufferedUnacknowledgedRenderBatches = 10;
    });

var app = builder.Build();

var cspImgSrcAllowlist = builder.Configuration
    .GetSection("Security:Csp:ImgSrc")
    .Get<string[]>() ?? Array.Empty<string>();

var cspImgSrcDirective = "img-src 'self' data: blob:" +
    (cspImgSrcAllowlist.Length > 0 ? " " + string.Join(" ", cspImgSrcAllowlist) : "");

// Build connect-src directive to include API URL
var apiBaseUrl = apiSettings?.BaseUrl ?? "";
var apiOrigin = "";
if (!string.IsNullOrEmpty(apiBaseUrl))
{
    try
    {
        var uri = new Uri(apiBaseUrl);
        apiOrigin = $"{uri.Scheme}://{uri.Host}";
    }
    catch { }
}
var cspConnectSrcDirective = string.IsNullOrEmpty(apiOrigin) 
    ? "connect-src 'self'" 
    : $"connect-src 'self' {apiOrigin}";

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();

// ✅ Enable routing middleware (required for MapControllers)
app.UseRouting();

// Security Headers (SOC 2 Compliance)
app.Use(async (context, next) =>
{
    if (!app.Environment.IsDevelopment())
    {
        context.Response.Headers.Add("X-Frame-Options", "DENY");
        context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
        context.Response.Headers.Add("Permissions-Policy", "geolocation=(), microphone=(), camera=()");
        context.Response.Headers.Add("Content-Security-Policy", 
            $"default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; {cspImgSrcDirective}; {cspConnectSrcDirective}; frame-ancestors 'none';");
    }
    await next();
});

app.UseAntiforgery();

// ✅ Document proxy endpoint - Forwards browser requests to API (bypasses IP restrictions)
app.MapDocumentProxy();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies();

app.Run();
