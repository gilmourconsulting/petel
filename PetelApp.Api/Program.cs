using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;  // ✅ ADD THIS - Required for IOptions<T>
using PetelApp.Api.Configuration;  // ✅ ADD THIS - Required for DatabaseSettings

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
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
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

// Register school attribute services
builder.Services.AddSingleton<SchoolAttributeCache>();
builder.Services.AddHostedService<SchoolAttributeLoaderHostedService>();

// User Session Management (Token-based)
builder.Services.AddSingleton<UserSessionService>();

// Business Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<UserRoleService>();

// Register file processor service
builder.Services.AddScoped<StudentsFileProcessor>();

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



// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseCors("AllowFrontend");
app.UseSession();

// ❌ REMOVE THESE - No authentication middleware needed
// app.UseAuthentication();
// app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new HangfireAuthorizationFilter() }
    });
}

app.MapControllers();

app.Run();

// Ensure logs are flushed on shutdown
Log.CloseAndFlush();