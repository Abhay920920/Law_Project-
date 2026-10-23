using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace MVCCaseManagement.Utils.Security
{
    /// <summary>
    /// Enterprise Cryptographic Utility for protecting sensitive credentials in appsettings.json
    /// and obfuscated assembly configurations using AES-256-GCM authenticated encryption.
    /// </summary>
    public static class SecretProtector
    {
        private const string Prefix = "ENC(";
        private const string Suffix = ")";
        private const int NonceSize = 12; // 96 bits for AES-GCM
        private const int TagSize = 16;   // 128 bits authentication tag

        // Stable salt for key derivation when environment variable is not explicitly injected
        private static readonly byte[] SystemSalt = Encoding.UTF8.GetBytes("NWKRTC_LAW_SUITE_PROTECTION_2026_SALT");

        /// <summary>
        /// Checks whether a given configuration string value is encrypted.
        /// </summary>
        public static bool IsEncrypted(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            var trimmed = value.Trim();
            return trimmed.StartsWith(Prefix, StringComparison.Ordinal) && 
                   trimmed.EndsWith(Suffix, StringComparison.Ordinal) && 
                   trimmed.Length > (Prefix.Length + Suffix.Length);
        }

        /// <summary>
        /// Encrypts a plain-text secret using AES-256-GCM.
        /// Returns a formatted token: ENC(<base64_payload>)
        /// </summary>
        public static string Encrypt(string plainText, string? customKey = null)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;

            byte[] keyBytes = ResolveMasterKey(customKey);
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
            byte[] tag = new byte[TagSize];
            byte[] cipherBytes = new byte[plainBytes.Length];

            using var aesGcm = new AesGcm(keyBytes, TagSize);
            aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);

            // Combined payload: [12-byte Nonce] + [16-byte Tag] + [Ciphertext]
            byte[] payload = new byte[NonceSize + TagSize + cipherBytes.Length];
            Buffer.BlockCopy(nonce, 0, payload, 0, NonceSize);
            Buffer.BlockCopy(tag, 0, payload, NonceSize, TagSize);
            Buffer.BlockCopy(cipherBytes, 0, payload, NonceSize + TagSize, cipherBytes.Length);

            return $"{Prefix}{Convert.ToBase64String(payload)}{Suffix}";
        }

        /// <summary>
        /// Decrypts a secret formatted as ENC(<base64_payload>) using AES-256-GCM.
        /// If the value is not encrypted, returns the original value.
        /// </summary>
        public static string Decrypt(string encryptedToken, string? customKey = null)
        {
            if (string.IsNullOrWhiteSpace(encryptedToken)) return encryptedToken;
            var trimmed = encryptedToken.Trim();
            if (!IsEncrypted(trimmed)) return encryptedToken;

            string base64Payload = trimmed.Substring(Prefix.Length, trimmed.Length - Prefix.Length - Suffix.Length);
            byte[] payload = Convert.FromBase64String(base64Payload);

            if (payload.Length < NonceSize + TagSize)
            {
                throw new CryptographicException("Invalid encrypted payload size: corrupt data.");
            }

            byte[] keyBytes = ResolveMasterKey(customKey);
            byte[] nonce = new byte[NonceSize];
            byte[] tag = new byte[TagSize];
            int cipherSize = payload.Length - NonceSize - TagSize;
            byte[] cipherBytes = new byte[cipherSize];
            byte[] plainBytes = new byte[cipherSize];

            Buffer.BlockCopy(payload, 0, nonce, 0, NonceSize);
            Buffer.BlockCopy(payload, NonceSize, tag, 0, TagSize);
            Buffer.BlockCopy(payload, NonceSize + TagSize, cipherBytes, 0, cipherSize);

            using var aesGcm = new AesGcm(keyBytes, TagSize);
            aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);

            return Encoding.UTF8.GetString(plainBytes);
        }

        /// <summary>
        /// Recursively discovers all ENC(...) values in the configuration and replaces them
        /// with decrypted plain-text in-memory, leaving disk files securely encrypted.
        /// </summary>
        public static void DecryptConfiguration(this ConfigurationManager configuration, string? customKey = null)
        {
            var decryptedEntries = new Dictionary<string, string?>();

            foreach (var pair in configuration.AsEnumerable())
            {
                if (!string.IsNullOrEmpty(pair.Value) && IsEncrypted(pair.Value))
                {
                    try
                    {
                        string decrypted = Decrypt(pair.Value, customKey);
                        decryptedEntries[pair.Key] = decrypted;
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[SecretProtector] Error decrypting config key '{pair.Key}': {ex.Message}");
                        Console.ResetColor();
                    }
                }
            }

            if (decryptedEntries.Count > 0)
            {
                configuration.AddInMemoryCollection(decryptedEntries);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[SecretProtector] Successfully decrypted {decryptedEntries.Count} secure credential(s) in-memory.");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Resolves the 256-bit AES Master Key from:
        /// 1. Custom key parameter if explicitly provided.
        /// 2. NWKRTC_MASTER_KEY environment variable.
        /// 3. Hardware-bound Machine GUID / Host derivation seed.
        /// </summary>
        private static byte[] ResolveMasterKey(string? customKey)
        {
            if (!string.IsNullOrWhiteSpace(customKey))
            {
                using var sha = SHA256.Create();
                return sha.ComputeHash(Encoding.UTF8.GetBytes(customKey));
            }

            string? envKey = Environment.GetEnvironmentVariable("NWKRTC_MASTER_KEY");
            if (!string.IsNullOrWhiteSpace(envKey))
            {
                using var sha = SHA256.Create();
                return sha.ComputeHash(Encoding.UTF8.GetBytes(envKey));
            }

            // Derive deterministic 256-bit key using machine-level identifiers + hardware salt
            string seed = $"{Environment.MachineName}_NWKRTC_LEGAL_SECURE_VAULT_KEY_{Environment.UserName}";
            using var rfc = new Rfc2898DeriveBytes(seed, SystemSalt, 100_000, HashAlgorithmName.SHA256);
            return rfc.GetBytes(32); // 256-bit AES key
        }
    }
}
