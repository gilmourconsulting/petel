using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Petel.Core.Abstractions;
using Petel.Core.Security;
using Petel.Core.Session;
using PetelAssistants.Api.Data;
using PetelAssistants.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.Configure<DatabaseSettings>(
    builder.Configuration.GetSection("Database"));
builder.Services.Configure<SecuritySettings>(
    builder.Configuration.GetSection("Security"));

builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
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

builder.Services.AddSingleton<SystemAttributeCache>();
builder.Services.AddSingleton<IAttributeCache>(sp => sp.GetRequiredService<SystemAttributeCache>());
builder.Services.AddSingleton<UserSessionService>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddHostedService<SystemAttributeLoaderHostedService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var jwtService = scope.ServiceProvider.GetRequiredService<JwtTokenService>();
    var sessionService = scope.ServiceProvider.GetRequiredService<UserSessionService>();
    sessionService.SetJwtTokenService(jwtService);
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapControllers();

app.Run();
