using PetelAssistants.BlazorServer.Components;
using Petel.BlazorCore.Models;
using Petel.BlazorCore.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<ApiSettings>(
    builder.Configuration.GetSection("ApiSettings"));

builder.Services.Configure<Petel.BlazorCore.Models.ApiSettings>(
    builder.Configuration.GetSection("ApiSettings"));

var apiSettings = builder.Configuration.GetSection("ApiSettings").Get<ApiSettings>();
builder.Services.AddHttpClient("PetelApi", client =>
{
    client.BaseAddress = new Uri(apiSettings?.BaseUrl ?? "http://localhost:5238/api");
    client.Timeout = TimeSpan.FromSeconds(apiSettings?.Timeout ?? 30);
});

builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<AuthenticationService>();
builder.Services.AddScoped<ApiService>();
builder.Services.AddScoped<SessionStateService>();
builder.Services.AddScoped<SessionTimeoutService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
