using System;
using Xunit;
using MVCCaseManagement.Common;

namespace MVCCaseManagement.Tests
{
    public class PasswordHelperTests
    {
        [Fact]
        public void HashPassword_GeneratesValidPbkdf2Hash()
        {
            string password = "TestPassword@123";
            string hash = PasswordHelper.HashPassword(password);

            Assert.NotNull(hash);
            Assert.StartsWith("AQAAAA", hash);
            Assert.True(PasswordHelper.VerifyPassword(password, hash));
        }

        [Theory]
        [InlineData("CorrectPassword123")]
        [InlineData("Admin@NWKRTC#2026")]
        [InlineData("Special!@#$%^&*()_+")]
        public void VerifyPassword_ValidPbkdf2Password_ReturnsTrue(string password)
        {
            string hash = PasswordHelper.HashPassword(password);
            bool isValid = PasswordHelper.VerifyPassword(password, hash);

            Assert.True(isValid);
        }

        [Fact]
        public void VerifyPassword_WrongPassword_ReturnsFalse()
        {
            string password = "CorrectPassword123";
            string hash = PasswordHelper.HashPassword(password);
            bool isValid = PasswordHelper.VerifyPassword("WrongPassword456", hash);

            Assert.False(isValid);
        }

        [Fact]
        public void VerifyPassword_LegacySha256_ReturnsTrueForMatchingPassword()
        {
            // Compute SHA256 of "secret"
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            byte[] bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes("secret"));
            string legacyHash = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();

            bool isValid = PasswordHelper.VerifyPassword("secret", legacyHash);
            Assert.True(isValid);

            bool isWrongValid = PasswordHelper.VerifyPassword("wrong", legacyHash);
            Assert.False(isWrongValid);
        }

        [Theory]
        [InlineData(null, "$pbkdf2$10000$salt$hash")]
        [InlineData("pass", null)]
        [InlineData("", "")]
        public void VerifyPassword_NullOrEmptyInputs_ReturnsFalse(string? password, string? hash)
        {
            bool isValid = PasswordHelper.VerifyPassword(password!, hash!);
            Assert.False(isValid);
        }
    }
}
