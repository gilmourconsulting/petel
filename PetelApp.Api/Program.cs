using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options; 
using PetelApp.Api.Configuration;  

using PetelApp.Api.Data;
using PetelApp.Api.Services;
using Hangfire;
using Hangfire.PostgreSql;
using PetelApp.Api.Session;
using Serilog;
using System.IO;



var builder = WebApplication.CreateBuilder(args);

var logsPath = Path.Combine(Directory.GetCurrentDirectory(), "logs");
if (!Directory.Exists(logsPath))
{
    Directory.CreateDirectory(logsPath);
}

// Configure Serilog for file logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console()
    .WriteTo.File(
        path: Path.Combine(logsPath, "petelapp-.txt"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}"
    )
    .CreateLogger();
// Use Serilog instead of default logging
builder.Host.UseSerilog();


// Add services
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        // Preserve Hebrew and special characters
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
builder.Services.Configure<DatabaseSettings>(
    builder.Configuration.GetSection("Database"));
builder.Services.Configure<SecuritySettings>(
    builder.Configuration.GetSection("Security"));

builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    var dbSettings = serviceProvider.GetRequiredService<IOptions<DatabaseSettings>>().Value;
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions => npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", dbSettings.SchemaName)
    );
});

// Session - ASP.NET Core session (minimal use)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(24);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".PetelApp.Session";
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
              "http://localhost:3000", 
              "http://localhost:5173",
              "https://petel-test-api-ahafcqfnh6drcdbd.israelcentral-01.azurewebsites.net",
              "https://petel.site")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// System Attributes (Global Config)
builder.Services.AddSingleton<SystemAttributeCache>();
builder.Services.AddHostedService<SystemAttributeLoaderHostedService>();
builder.Services.AddScoped<SystemAttributeService>();
builder.Services.AddScoped<GlobalFunctions>();

builder.Services.AddScoped<StudentPricingService>();
builder.Services.AddScoped<StudentService>();


// Register school attribute services
builder.Services.AddSingleton<SchoolAttributeCache>();
builder.Services.AddHostedService<SchoolAttributeLoaderHostedService>();

// User Session Management (Token-based)
builder.Services.AddSingleton<UserSessionService>();

// JWT Token Service (must be singleton to match UserSessionService)
builder.Services.AddSingleton<JwtTokenService>();

// Business Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<UserRoleService>();

// Register file processor service
builder.Services.AddScoped<StudentsFileProcessor>();

builder.Services.AddSingleton<ActionAuthorizationService>();

// Hangfire (if used)
var hangfireConnectionString = builder.Configuration.GetConnectionString("HangfireConnection");
if (!string.IsNullOrEmpty(hangfireConnectionString))
{
    builder.Services.AddHangfire(config =>
        config.UsePostgreSqlStorage(c => c.UseNpgsqlConnection(hangfireConnectionString)));
    builder.Services.AddHangfireServer();
}

builder.Services.AddLogging();

var app = builder.Build();

//  Initialize JWT service in UserSessionService
var sessionService = app.Services.GetRequiredService<UserSessionService>();
var jwtService = app.Services.GetRequiredService<JwtTokenService>();
sessionService.SetJwtTokenService(jwtService);

var authService = app.Services.GetRequiredService<ActionAuthorizationService>();
await authService.InitializeAsync();

app.UseStaticFiles();

// Configure pipeline
// Enable Swagger in Development and Test environments
if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "Test")
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseCors("AllowFrontend");
app.UseSession();


if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "Test")
{
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new HangfireAuthorizationFilter() }
    });
}

app.MapControllers();

// Serve index.html for non-API routes (SPA fallback)
// This allows direct navigation to /schooldetails, /students, etc.
app.MapFallback(async context =>
{
    // Don't use fallback for API requests, Swagger, or Hangfire
    if (context.Request.Path.StartsWithSegments("/api") ||
        context.Request.Path.StartsWithSegments("/swagger") ||
        context.Request.Path.StartsWithSegments("/hangfire"))
    {
        context.Response.StatusCode = 404;
        return;
    }
    
    // Serve index.html for all other routes
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync("wwwroot/index.html");
});

app.Run();

// Ensure logs are flushed on shutdown
Log.CloseAndFlush();