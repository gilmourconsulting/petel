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

// ✅ Register DataEncryptionService (BEFORE AddDbContext)
builder.Services.AddSingleton<DataEncryptionService>();

// ✅ Update DbContext registration to inject DataEncryptionService
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

// ✅ ADD THIS CODE HERE - Check for migration command BEFORE building app
if (args.Length > 0 && args[0] == "migrate-encrypt-data")
{
    Console.WriteLine("========================================");
    Console.WriteLine("STARTING DATA ENCRYPTION MIGRATION");
    Console.WriteLine("========================================");
    
    // Build service provider temporarily for migration
    var serviceProvider = builder.Services.BuildServiceProvider();
    
    using var scope = serviceProvider.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var encryption = scope.ServiceProvider.GetRequiredService<DataEncryptionService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    var totalErrors = 0;
    
    try
    {
        // Get database connection
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();
        
        // ===== Migrate Persons Table =====
        Console.WriteLine("\n📋 Migrating persons table...");
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT id, id_number, email, phone_number 
                FROM petel_schema.persons 
                WHERE (id_number IS NOT NULL AND id_number != '0' AND id_number != '') 
                   OR (email IS NOT NULL AND email != '') 
                   OR (phone_number IS NOT NULL AND phone_number != '')";
            
            var personsToUpdate = new List<(int id, string? idNumber, string? email, string? phone)>();
            
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var id = reader.GetInt32(0);
                    var idNumber = reader.IsDBNull(1) ? null : reader.GetString(1);
                    var email = reader.IsDBNull(2) ? null : reader.GetString(2);
                    var phone = reader.IsDBNull(3) ? null : reader.GetString(3);
                    
                    personsToUpdate.Add((id, idNumber, email, phone));
                }
            }
            
            Console.WriteLine($"  Found {personsToUpdate.Count} persons to encrypt...");
            
            // Encrypt and update
            var encrypted = 0;
            foreach (var person in personsToUpdate)
            {
                try
                {
                    var encIdNumber = person.idNumber != null && !IsBase64(person.idNumber) 
                        ? encryption.Encrypt(person.idNumber) 
                        : person.idNumber;
                    
                    var encEmail = person.email != null && !IsBase64(person.email)
                        ? encryption.Encrypt(person.email)
                        : person.email;
                    
                    var encPhone = person.phone != null && !IsBase64(person.phone)
                        ? encryption.Encrypt(person.phone)
                        : person.phone;
                    
                    using (var updateCmd = connection.CreateCommand())
                    {
                        updateCmd.CommandText = @"
                            UPDATE petel_schema.persons 
                            SET id_number = @idNumber, 
                                email = @email, 
                                phone_number = @phone
                            WHERE id = @id";
                        
                        var p1 = updateCmd.CreateParameter();
                        p1.ParameterName = "@idNumber";
                        p1.Value = (object?)encIdNumber ?? DBNull.Value;
                        updateCmd.Parameters.Add(p1);
                        
                        var p2 = updateCmd.CreateParameter();
                        p2.ParameterName = "@email";
                        p2.Value = (object?)encEmail ?? DBNull.Value;
                        updateCmd.Parameters.Add(p2);
                        
                        var p3 = updateCmd.CreateParameter();
                        p3.ParameterName = "@phone";
                        p3.Value = (object?)encPhone ?? DBNull.Value;
                        updateCmd.Parameters.Add(p3);
                        
                        var p4 = updateCmd.CreateParameter();
                        p4.ParameterName = "@id";
                        p4.Value = person.id;
                        updateCmd.Parameters.Add(p4);
                        
                        await updateCmd.ExecuteNonQueryAsync();
                    }
                    
                    encrypted++;
                    if (encrypted % 100 == 0)
                        Console.WriteLine($"  Processed {encrypted} persons...");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"Error encrypting Person ID {person.id}");
                    totalErrors++;
                }
            }
            
            Console.WriteLine($"✅ Persons encrypted: {encrypted}");
        }
        
        // ===== Migrate School Students Table =====
        Console.WriteLine("\n📋 Migrating school_students table...");
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT id, id_number, street 
                FROM petel_schema.school_students 
                WHERE (id_number IS NOT NULL AND id_number != '') 
                   OR (street IS NOT NULL AND street != '')";
            
            var studentsToUpdate = new List<(int id, string? idNumber, string? street)>();
            
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var id = reader.GetInt32(0);
                    var idNumber = reader.IsDBNull(1) ? null : reader.GetString(1);
                    var street = reader.IsDBNull(2) ? null : reader.GetString(2);
                    
                    studentsToUpdate.Add((id, idNumber, street));
                }
            }
            
            Console.WriteLine($"  Found {studentsToUpdate.Count} students to encrypt...");
            
            var encrypted = 0;
            foreach (var student in studentsToUpdate)
            {
                try
                {
                    var encIdNumber = student.idNumber != null && !IsBase64(student.idNumber)
                        ? encryption.Encrypt(student.idNumber)
                        : student.idNumber;
                    
                    var encStreet = student.street != null && !IsBase64(student.street)
                        ? encryption.Encrypt(student.street)
                        : student.street;
                    
                    using (var updateCmd = connection.CreateCommand())
                    {
                        updateCmd.CommandText = @"
                            UPDATE petel_schema.school_students 
                            SET id_number = @idNumber, street = @street
                            WHERE id = @id";
                        
                        var p1 = updateCmd.CreateParameter();
                        p1.ParameterName = "@idNumber";
                        p1.Value = (object?)encIdNumber ?? DBNull.Value;
                        updateCmd.Parameters.Add(p1);
                        
                        var p2 = updateCmd.CreateParameter();
                        p2.ParameterName = "@street";
                        p2.Value = (object?)encStreet ?? DBNull.Value;
                        updateCmd.Parameters.Add(p2);
                        
                        var p3 = updateCmd.CreateParameter();
                        p3.ParameterName = "@id";
                        p3.Value = student.id;
                        updateCmd.Parameters.Add(p3);
                        
                        await updateCmd.ExecuteNonQueryAsync();
                    }
                    
                    encrypted++;
                    if (encrypted % 100 == 0)
                        Console.WriteLine($"  Processed {encrypted} students...");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"Error encrypting SchoolStudent ID {student.id}");
                    totalErrors++;
                }
            }
            
            Console.WriteLine($"✅ Students encrypted: {encrypted}");
        }
        
        // ===== Migrate Users Table =====
        Console.WriteLine("\n📋 Migrating users table...");
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT id, otp_secret, email 
                FROM petel_schema.users 
                WHERE (otp_secret IS NOT NULL AND otp_secret != '') 
                   OR (email IS NOT NULL AND email != '')";
            
            var usersToUpdate = new List<(int id, string? otpSecret, string? email)>();
            
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var id = reader.GetInt32(0);
                    var otpSecret = reader.IsDBNull(1) ? null : reader.GetString(1);
                    var email = reader.IsDBNull(2) ? null : reader.GetString(2);
                    
                    usersToUpdate.Add((id, otpSecret, email));
                }
            }
            
            Console.WriteLine($"  Found {usersToUpdate.Count} users to encrypt...");
            
            var encrypted = 0;
            foreach (var user in usersToUpdate)
            {
                try
                {
                    var encOtpSecret = user.otpSecret != null && !IsBase64(user.otpSecret)
                        ? encryption.Encrypt(user.otpSecret)
                        : user.otpSecret;
                    
                    var encEmail = user.email != null && !IsBase64(user.email)
                        ? encryption.Encrypt(user.email)
                        : user.email;
                    
                    using (var updateCmd = connection.CreateCommand())
                    {
                        updateCmd.CommandText = @"
                            UPDATE petel_schema.users 
                            SET otp_secret = @otpSecret, email = @email
                            WHERE id = @id";
                        
                        var p1 = updateCmd.CreateParameter();
                        p1.ParameterName = "@otpSecret";
                        p1.Value = (object?)encOtpSecret ?? DBNull.Value;
                        updateCmd.Parameters.Add(p1);
                        
                        var p2 = updateCmd.CreateParameter();
                        p2.ParameterName = "@email";
                        p2.Value = (object?)encEmail ?? DBNull.Value;
                        updateCmd.Parameters.Add(p2);
                        
                        var p3 = updateCmd.CreateParameter();
                        p3.ParameterName = "@id";
                        p3.Value = user.id;
                        updateCmd.Parameters.Add(p3);
                        
                        await updateCmd.ExecuteNonQueryAsync();
                    }
                    
                    encrypted++;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"Error encrypting User ID {user.id}");
                    totalErrors++;
                }
            }
            
            Console.WriteLine($"✅ Users encrypted: {encrypted}");
        }
        
        await connection.CloseAsync();
        
        Console.WriteLine("\n========================================");
        Console.WriteLine($"MIGRATION COMPLETE");
        Console.WriteLine($"Total errors: {totalErrors}");
        Console.WriteLine("========================================");
        
        return;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n❌ MIGRATION FAILED: {ex.Message}");
        logger.LogError(ex, "Data encryption migration failed");
        return;
    }
    
    static bool IsBase64(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (value.Length < 20) return false; // Encrypted values are always longer
        try
        {
            Convert.FromBase64String(value);
            return value.Contains("=") || value.Length % 4 == 0; // Basic base64 check
        }
        catch
        {
            return false;
        }
    }
}


// Add test-decrypt command for troubleshooting
if (args.Length > 0 && args[0] == "test-decrypt")
{
    if (args.Length < 2)
    {
        Console.WriteLine("Usage: dotnet run -- test-decrypt <encrypted-value>");
        return;
    }
    
    var serviceProvider = builder.Services.BuildServiceProvider();
    using var scope = serviceProvider.CreateScope();
    var encryption = scope.ServiceProvider.GetRequiredService<DataEncryptionService>();
    
    try
    {
        var encryptedValue = args[1];
        Console.WriteLine("========================================");
        Console.WriteLine("DECRYPTION TEST");
        Console.WriteLine("========================================");
        Console.WriteLine($"Encrypted value: {encryptedValue.Substring(0, Math.Min(50, encryptedValue.Length))}...");
        Console.WriteLine($"Length: {encryptedValue.Length} characters");
        Console.WriteLine("");
        
        var decrypted = encryption.Decrypt(encryptedValue);
        
        Console.WriteLine("✅ DECRYPTION SUCCESSFUL");
        Console.WriteLine($"Decrypted value: {decrypted}");
        Console.WriteLine("========================================");
    }
    catch (Exception ex)
    {
        Console.WriteLine("❌ DECRYPTION FAILED");
        Console.WriteLine($"Error: {ex.Message}");
        Console.WriteLine($"Type: {ex.GetType().Name}");
        Console.WriteLine("========================================");
    }
    
    return;
}

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


// ✅ Only enable Hangfire dashboard if Hangfire is configured
if (!string.IsNullOrEmpty(hangfireConnectionString) && 
    (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "Test"))
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

// ✅ Add before app.Run() for key generation utility
if (args.Length > 0 && args[0] == "generate-encryption-key")
{
    var newKey = DataEncryptionService.GenerateEncryptionKey();
    Console.WriteLine("==============================================");
    Console.WriteLine("NEW ENCRYPTION KEY (Base64):");
    Console.WriteLine(newKey);
    Console.WriteLine("==============================================");
    Console.WriteLine("IMPORTANT: Store this key in Azure Key Vault:");
    Console.WriteLine($"az keyvault secret set --vault-name petel-kv-test-4721 --name DataEncryption--EncryptionKey --value \"{newKey}\"");
    Console.WriteLine("==============================================");
    return;
}

app.Run();

// Ensure logs are flushed on shutdown
Log.CloseAndFlush();