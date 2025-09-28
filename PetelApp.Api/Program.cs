using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;
using PetelApp.Api.Services;
using PetelApp.Api.Session;
using PetelApp.Api.Middleware;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add HttpContextAccessor for session management
builder.Services.AddHttpContextAccessor();

// Database context with PostgreSQL following database conventions
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Session services following authentication & session management patterns
builder.Services.AddSingleton<UserSessionService>();
builder.Services.AddSingleton<SystemAttributeService>();


builder.Services.AddScoped<SystemAttributeService>();

// Background services for system attributes loading
builder.Services.AddHostedService<SystemAttributeLoaderHostedService>();

// CORS following security patterns
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://127.0.0.1:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// CRITICAL: Configure session following Authentication & Session Management
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8); // 8-hour session for school day
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = "PetelSession";
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// Configure Hangfire with PostgreSQL storage
builder.Services.AddHangfire(config => 
{
    config.SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
          .UseSimpleAssemblyNameTypeSerializer()
          .UseRecommendedSerializerSettings()
          .UsePostgreSqlStorage(options =>
          {
              options.UseNpgsqlConnection(builder.Configuration.GetConnectionString("DefaultConnection"));
          });
});
builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = Environment.ProcessorCount * 2;
    options.Queues = new[] { "default", "system" };
});

// Register auth and user role services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddSingleton<UserSessionService>();
builder.Services.AddScoped<UserRoleService>();
builder.Services.AddHangfireServer();

var app = builder.Build();

// Configure pipeline following critical development workflows
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");

// Verify database connection at startup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        await context.Database.CanConnectAsync();
        var entityCount = await context.Entities.CountAsync();
        var attributeCount = await context.SystemAttributes.CountAsync();
        Console.WriteLine($"Database connected - {entityCount} entities, {attributeCount} system attributes available");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Database connection failed: {ex.Message}");
        throw;
    }
}


app.UseSession();



// Update middleware configuration
app.UseAuthentication();
app.UseAuthorization();

// Use top-level route registrations
app.MapControllers();
app.MapHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() }
});

// Configure Hangfire dashboard and server
app.UseHangfireDashboard();


Console.WriteLine("Petel Educational Management System API started - data will be loaded from database");
app.Run();