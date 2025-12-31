using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PetelApp.Api.Data;
using PetelApp.Api.Services;
using System;
using System.Linq;
using System.Threading.Tasks;


// PetelApp.Api/Services/DataMigrationService.cs
public class DataMigrationService
{
    private readonly AppDbContext _context;
    private readonly DataEncryptionService _encryption;
    private readonly ILogger<DataMigrationService> _logger;

    public DataMigrationService(
        AppDbContext context,
        DataEncryptionService encryption,
        ILogger<DataMigrationService> logger)
    {
        _context = context;
        _encryption = encryption;
        _logger = logger;
    }

    /// <summary>
    /// One-time migration: Encrypt all existing plaintext sensitive data
    /// RUN THIS ONCE after deploying encryption changes
    /// </summary>
    public async Task<(int encrypted, int errors)> EncryptExistingDataAsync()
    {
        var encrypted = 0;
        var errors = 0;

        try
        {
            // Disable change tracking for bulk update
            _context.ChangeTracker.AutoDetectChangesEnabled = false;

            // Encrypt Person records
            var persons = await _context.Set<Person>()
                .Where(p => p.IdNumber != null || p.Email != null || p.PhoneNumber != null)
                .ToListAsync();

            foreach (var person in persons)
            {
                try
                {
                    // Check if already encrypted (base64 check)
                    if (person.IdNumber != null && !IsBase64(person.IdNumber))
                    {
                        person.IdNumber = _encryption.Encrypt(person.IdNumber);
                    }
                    if (person.Email != null && !IsBase64(person.Email))
                    {
                        person.Email = _encryption.Encrypt(person.Email);
                    }
                    if (person.PhoneNumber != null && !IsBase64(person.PhoneNumber))
                    {
                        person.PhoneNumber = _encryption.Encrypt(person.PhoneNumber);
                    }
                    encrypted++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error encrypting Person ID {person.Id}");
                    errors++;
                }
            }

            // Encrypt SchoolStudent records
            var students = await _context.Set<SchoolStudent>()
                .Where(s => s.IdNumber != null || s.Street != null)
                .ToListAsync();

            foreach (var student in students)
            {
                try
                {
                    if (student.IdNumber != null && !IsBase64(student.IdNumber))
                    {
                        student.IdNumber = _encryption.Encrypt(student.IdNumber);
                    }
                    if (student.Street != null && !IsBase64(student.Street))
                    {
                        student.Street = _encryption.Encrypt(student.Street);
                    }
                    encrypted++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error encrypting SchoolStudent ID {student.Id}");
                    errors++;
                }
            }

            // Encrypt User OTP secrets
            var users = await _context.Set<User>()
                .Where(u => u.OtpSecret != null)
                .ToListAsync();

            foreach (var user in users)
            {
                try
                {
                    if (user.OtpSecret != null && !IsBase64(user.OtpSecret))
                    {
                        user.OtpSecret = _encryption.Encrypt(user.OtpSecret);
                    }
                    if (user.Email != null && !IsBase64(user.Email))
                    {
                        user.Email = _encryption.Encrypt(user.Email);
                    }
                    encrypted++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error encrypting User ID {user.Id}");
                    errors++;
                }
            }

            await _context.SaveChangesAsync();
            _context.ChangeTracker.AutoDetectChangesEnabled = true;

            _logger.LogInformation($"Migration complete: {encrypted} records encrypted, {errors} errors");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error during data migration");
            errors++;
        }

        return (encrypted, errors);
    }

    private static bool IsBase64(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        try
        {
            Convert.FromBase64String(value);
            return true;
        }
        catch
        {
            return false;
        }
    }
}