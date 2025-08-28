using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;
using PetelApp.Api.Services;        
using PetelApp.Api.Middleware;      

var builder = WebApplication.CreateBuilder(args);

// Add basic services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add database context
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add your services - register both interface and concrete class
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<PetelApp.Api.Session.UserSessionService>();
builder.Services.AddScoped<TenantService>(); // For controllers injecting concrete class
builder.Services.AddScoped<ITenantService, TenantService>(); // For controllers injecting interface
builder.Services.AddSingleton<SystemAttributeService>();
builder.Services.AddHostedService<SystemAttributeLoaderHostedService>();

// Simple CORS for testing
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("X-Tenant-ID");
    });
});

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(1);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Simple pipeline
app.UseRouting();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AllowAll");
app.UseSession();

app.UseAuthorization();

// Add tenant middleware
app.UseMiddleware<TenantMiddleware>();

app.MapControllers();

// Test endpoint
app.MapGet("/test", () => "API is working with database and services!");

app.Run();