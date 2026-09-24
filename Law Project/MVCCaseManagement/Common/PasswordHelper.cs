using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace MVCCaseManagement.Common
{
    public static class PasswordHelper
    {
        private static readonly PasswordHasher<string> _hasher = new PasswordHasher<string>();

        /// <summary>
        /// Hash password using modern salted PKBDF2 (via Identity PasswordHasher)
        /// </summary>
        public static string HashPassword(string password)
        {
            return _hasher.HashPassword("user", password);
        }

        /// <summary>
        /// Verify password against hash with legacy SHA256 fallback
        /// </summary>
        public static bool VerifyPassword(string password, string hash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash)) return false;

            // 1. Try modern verification (PBKDF2)
            try
            {
                var result = _hasher.VerifyHashedPassword("user", hash, password);
                if (result == PasswordVerificationResult.Success) return true;
                if (result == PasswordVerificationResult.SuccessRehashNeeded) 
                {
                    // In a production system, we would trigger a rehash here.
                    return true;
                }
            }
            catch
            {
                // If it's not a valid Identity hash, it might be a legacy hash
            }

            // 2. Legacy Fallback (SHA256)
            var legacyHash = HashPasswordLegacy(password);
            return legacyHash.Equals(hash, StringComparison.OrdinalIgnoreCase);
        }

        private static string HashPasswordLegacy(string password)
        {
            if (string.IsNullOrEmpty(password)) return "";
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return BitConverter.ToString(bytes).Replace("-", "").ToUpper();
        }
    }

    public static class SessionKeys
    {
        public const string UserID = "UserID";
        public const string Username = "Username";
        public const string FullName = "FullName";
        public const string RoleID = "RoleID";
        public const string RoleName = "RoleName";
        public const string DivisionID = "DivisionID";
        public const string DivisionName = "DivisionName";
    }

    public static class Constants
    {
        public const string AdminRole = "Admin";
        public const string DivisionUserRole = "Division User";
        
        public const string PendingStatus = "Pending";
        public const string DisposedStatus = "Disposed";
    }
}
