using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using PetelApp.Api.Configuration;

namespace PetelApp.Api.Services
{
    /// <summary>
    /// Provides AES-256 encryption/decryption for sensitive data fields
    /// Uses encryption key from Azure Key Vault in production/test
    /// </summary>
    public class DataEncryptionService
    {
        private readonly byte[] _encryptionKey;
        private readonly ILogger<DataEncryptionService> _logger;

        public DataEncryptionService(
            IOptions<SecuritySettings> securitySettings,
            ILogger<DataEncryptionService> logger)
        {
            _logger = logger;

            var keyString = securitySettings.Value.DataEncryption?.EncryptionKey;
            
            if (string.IsNullOrWhiteSpace(keyString))
            {
                throw new InvalidOperationException(
                    "Data encryption key not configured. Add 'Security:DataEncryption:EncryptionKey' to configuration.");
            }

            // Convert base64 key to bytes (key should be 32 bytes for AES-256)
            try
            {
                _encryptionKey = Convert.FromBase64String(keyString);
                
                if (_encryptionKey.Length != 32)
                {
                    throw new InvalidOperationException(
                        $"Encryption key must be 32 bytes (256 bits) for AES-256. Current length: {_encryptionKey.Length}");
                }
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException(
                    "Invalid encryption key format. Key must be base64-encoded 32-byte string.", ex);
            }

            _logger.LogInformation("DataEncryptionService initialized successfully");
        }

        /// <summary>
        /// Encrypts plaintext using AES-256-CBC with random IV
        /// Returns base64-encoded string: [IV:16bytes][Ciphertext:variable]
        /// </summary>
        public string Encrypt(string plaintext)
        {
            if (string.IsNullOrWhiteSpace(plaintext))
            {
                return plaintext;
            }

            try
            {
                using var aes = Aes.Create();
                aes.Key = _encryptionKey;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.GenerateIV(); // Random IV for each encryption

                using var encryptor = aes.CreateEncryptor();
                var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
                var ciphertextBytes = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

                // Prepend IV to ciphertext for decryption
                var result = new byte[aes.IV.Length + ciphertextBytes.Length];
                Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
                Buffer.BlockCopy(ciphertextBytes, 0, result, aes.IV.Length, ciphertextBytes.Length);

                return Convert.ToBase64String(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error encrypting data");
                throw new InvalidOperationException("Data encryption failed", ex);
            }
        }

   /// <summary>
/// Decrypts base64-encoded ciphertext encrypted with Encrypt() method
/// Handles legacy unencrypted data by returning it as-is
/// Extracts IV from first 16 bytes, decrypts remaining bytes
/// </summary>
public string Decrypt(string ciphertext)
{
    if (string.IsNullOrWhiteSpace(ciphertext))
    {
        return ciphertext;
    }

    try
    {
        // ✅ Try to decode as base64 first
        var fullCipher = Convert.FromBase64String(ciphertext);
        
        // ✅ Check if length is valid (at least IV + 1 block = 32 bytes)
        if (fullCipher.Length < 32)
        {
            _logger.LogWarning("Data too short to be encrypted (length: {Length}), treating as plain text", fullCipher.Length);
            return ciphertext; // Return as plain text
        }

        using var aes = Aes.Create();
        aes.Key = _encryptionKey;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        // Extract IV from first 16 bytes
        var iv = new byte[16];
        var ciphertextBytes = new byte[fullCipher.Length - 16];
        
        Buffer.BlockCopy(fullCipher, 0, iv, 0, 16);
        Buffer.BlockCopy(fullCipher, 16, ciphertextBytes, 0, ciphertextBytes.Length);

        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        var plaintextBytes = decryptor.TransformFinalBlock(ciphertextBytes, 0, ciphertextBytes.Length);

        return Encoding.UTF8.GetString(plaintextBytes);
    }
    catch (FormatException ex)
    {
        // ✅ Not base64 - treat as legacy plain text
        _logger.LogWarning(ex, "Data is not base64-encoded, treating as plain text: {Data}", 
            ciphertext?.Length > 50 ? ciphertext.Substring(0, 50) + "..." : ciphertext);
        return ciphertext;
    }
    catch (CryptographicException ex)
    {
        // ✅ Decryption failed - might be corrupted or plain text
        _logger.LogWarning(ex, "Cryptographic error during decryption, treating as plain text");
        return ciphertext;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error decrypting data");
        throw new InvalidOperationException("Data decryption failed", ex);
    }
}

        /// <summary>
        /// Generates a new random 256-bit encryption key (base64-encoded)
        /// Use this once to generate key, then store in Azure Key Vault
        /// </summary>
        public static string GenerateEncryptionKey()
        {
            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.GenerateKey();
            return Convert.ToBase64String(aes.Key);
        }
    }
}