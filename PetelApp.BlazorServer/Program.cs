using PetelApp.BlazorServer.Components;
using PetelApp.BlazorServer.Models;
using PetelApp.BlazorServer.Services;

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

// Add HTTP client and services
builder.Services.AddHttpClient();
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
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    // IMPORTANT: Azure App Service handles HTTPS termination at load balancer
    // Do NOT use HSTS or HTTPS redirection on App Service Linux
    // app.UseHsts();
    // app.UseHttpsRedirection();
}

// Azure App Service handles HTTPS - no redirect needed
// app.UseHttpsRedirection();


app.UseAntiforgery();

app.UseStaticFiles();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies();

app.Run();
