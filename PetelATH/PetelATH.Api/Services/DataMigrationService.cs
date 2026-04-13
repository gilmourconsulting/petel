using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PetelATH.Api.Data;
using PetelATH.Api.Services;
using System;
using System.Linq;
using System.Threading.Tasks;


// PetelATH.Api/Services/DataMigrationService.cs
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

    /// <summary>
    /// ⚠️ DO NOT USE - This migration corrupts data!
    /// The EF Core value converters automatically decrypt data when reading,
    /// so this tries to re-encrypt already-decrypted plain text.
    /// Use direct SQL migration instead.
    /// </summary>
    [Obsolete("This method corrupts data. Use SQL-based migration instead.")]
    public async Task<(int reencrypted, int errors)> MigrateToDeterministicEncryptionAsync()
    {
        throw new InvalidOperationException(
            "This migration method is disabled because it corrupts data. " +
            "EF Core value converters automatically decrypt when reading, causing double-encryption. " +
            "Please restore from backup and use the original encryption approach.");
    }

    /// <summary>
    /// Re-encrypts data that was encrypted with a different key
    /// Uses raw SQL to bypass EF Core value converters
    /// </summary>
    public async Task<(int reencrypted, int errors)> ReencryptWithOldKeyAsync(string oldKeyBase64, string tableName = "school_students", string columnName = "id_number")
    {
        var reencrypted = 0;
        var errors = 0;

        try
        {
            _logger.LogInformation($"Starting re-encryption for {tableName}.{columnName}");

            // Create encryption service with old key
            var oldKeyBytes = Convert.FromBase64String(oldKeyBase64);
            if (oldKeyBytes.Length != 32)
            {
                throw new InvalidOperationException($"Old key must be 32 bytes (256-bit). Got {oldKeyBytes.Length} bytes.");
            }

            // Get schema name from AppDbContext
            var connection = _context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            // Read raw encrypted data from database (bypassing EF Core converters)
            var selectSql = $"SELECT id, {columnName} FROM petel_schema.{tableName} WHERE {columnName} IS NOT NULL";
            using var selectCmd = connection.CreateCommand();
            selectCmd.CommandText = selectSql;

            var dataToReencrypt = new List<(int id, string encryptedValue)>();

            using (var reader = await selectCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var id = reader.GetInt32(0);
                    var encryptedValue = reader.IsDBNull(1) ? null : reader.GetString(1);
                    if (!string.IsNullOrWhiteSpace(encryptedValue))
                    {
                        dataToReencrypt.Add((id, encryptedValue));
                    }
                }
            }

            _logger.LogInformation($"Found {dataToReencrypt.Count} records to re-encrypt");

            // Re-encrypt each record
            foreach (var (id, encryptedValue) in dataToReencrypt)
            {
                try
                {
                    // Decrypt with old key
                    var plaintext = DecryptWithKey(encryptedValue, oldKeyBytes);

                    // Re-encrypt with current (production) key
                    var newEncryptedValue = _encryption.Encrypt(plaintext);

                    // Update database with raw SQL
                    var updateSql = $"UPDATE petel_schema.{tableName} SET {columnName} = @newValue WHERE id = @id";
                    using var updateCmd = connection.CreateCommand();
                    updateCmd.CommandText = updateSql;

                    var paramValue = updateCmd.CreateParameter();
                    paramValue.ParameterName = "@newValue";
                    paramValue.Value = newEncryptedValue;
                    updateCmd.Parameters.Add(paramValue);

                    var paramId = updateCmd.CreateParameter();
                    paramId.ParameterName = "@id";
                    paramId.Value = id;
                    updateCmd.Parameters.Add(paramId);

                    await updateCmd.ExecuteNonQueryAsync();

                    reencrypted++;

                    if (reencrypted % 100 == 0)
                    {
                        _logger.LogInformation($"Progress: {reencrypted}/{dataToReencrypt.Count} records re-encrypted");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error re-encrypting record ID {id}");
                    errors++;
                }
            }

            _logger.LogInformation($"Re-encryption complete: {reencrypted} records, {errors} errors");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error during re-encryption");
            errors++;
        }

        return (reencrypted, errors);
    }

    /// <summary>
    /// Decrypt data using a specific key (bypassing the configured encryption service)
    /// </summary>
    private string DecryptWithKey(string ciphertext, byte[] key)
    {
        if (string.IsNullOrWhiteSpace(ciphertext))
        {
            return ciphertext;
        }

        try
        {
            var fullCipher = Convert.FromBase64String(ciphertext);

            if (fullCipher.Length < 32)
            {
                throw new InvalidOperationException($"Encrypted data too short: {fullCipher.Length} bytes");
            }

            using var aes = System.Security.Cryptography.Aes.Create();
            aes.Key = key;
            aes.Mode = System.Security.Cryptography.CipherMode.CBC;
            aes.Padding = System.Security.Cryptography.PaddingMode.PKCS7;

            // Extract IV from first 16 bytes
            var iv = new byte[16];
            var ciphertextBytes = new byte[fullCipher.Length - 16];

            Buffer.BlockCopy(fullCipher, 0, iv, 0, 16);
            Buffer.BlockCopy(fullCipher, 16, ciphertextBytes, 0, ciphertextBytes.Length);

            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            var plaintextBytes = decryptor.TransformFinalBlock(ciphertextBytes, 0, ciphertextBytes.Length);

            return System.Text.Encoding.UTF8.GetString(plaintextBytes);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to decrypt with provided key: {ex.Message}", ex);
        }
    }
}