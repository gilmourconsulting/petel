using Microsoft.Extensions.Options;
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
app.MapGet("/api/documents/{documentId}/proxy", async (
    long documentId, 
    HttpContext httpContext,
    IHttpClientFactory httpClientFactory,
    IOptions<ApiSettings> apiSettings,
    ILogger<Program> logger) =>
{
    try
    {
        logger.LogInformation("📥 Document proxy request for ID: {DocumentId}", documentId);
        
        // Extract Authorization header from browser request
        if (!httpContext.Request.Headers.TryGetValue("Authorization", out var authHeader) ||
            string.IsNullOrEmpty(authHeader))
        {
            logger.LogWarning("⚠️ No authorization header in proxy request for document {DocumentId}", documentId);
            return Results.Unauthorized();
        }

        // Create HTTP client and forward browser's token to API
        var client = httpClientFactory.CreateClient("PetelApi");
        client.DefaultRequestHeaders.Add("Authorization", authHeader.ToString());
        
        var apiUrl = $"{apiSettings.Value.BaseUrl}/Documents/{documentId}/download";
        logger.LogDebug("Proxying request to: {ApiUrl}", apiUrl);
        
        var apiResponse = await client.GetAsync(apiUrl);
        
        if (!apiResponse.IsSuccessStatusCode)
        {
            logger.LogWarning("⚠️ API returned {StatusCode} for document {DocumentId}", 
                apiResponse.StatusCode, documentId);
            
            if (apiResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return Results.Unauthorized();
            
            if (apiResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                return Results.NotFound(new { error = "מסמך לא נמצא" });
            
            return Results.Problem($"שגיאה בטעינת המסמך: {apiResponse.StatusCode}");
        }
        
        var content = await apiResponse.Content.ReadAsByteArrayAsync();
        var contentType = apiResponse.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        
        // Extract filename from Content-Disposition header
        var fileName = $"document_{documentId}";
        if (apiResponse.Content.Headers.ContentDisposition?.FileName != null)
        {
            fileName = apiResponse.Content.Headers.ContentDisposition.FileName.Trim('"');
        }
        
        logger.LogInformation("✅ Returning document {DocumentId}, size: {Size} bytes, type: {ContentType}", 
            documentId, content.Length, contentType);
        
        return Results.File(content, contentType, fileName);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ Error proxying document {DocumentId}", documentId);
        return Results.Problem("שגיאה בטעינת המסמך");
    }
})
.DisableAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies();

app.Run();
