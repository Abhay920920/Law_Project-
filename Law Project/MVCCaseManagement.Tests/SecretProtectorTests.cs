using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using MVCCaseManagement.Utils.Security;
using Xunit;

namespace MVCCaseManagement.Tests
{
    public class SecretProtectorTests
    {
        [Theory]
        [InlineData("4c4H_0l8q")]
        [InlineData("Data Source=198.38.89.31;Initial Catalog=Admin_Law;User ID=admin_Law;Password=4c4H_0l8q;")]
        [InlineData("5763de722863865e2691e137691d4333")]
        [InlineData("Very$ecret!Key#2026")]
        public void EncryptAndDecrypt_RoundTrip_MatchesOriginal(string plainText)
        {
            // Act
            string encrypted = SecretProtector.Encrypt(plainText);

            // Assert
            Assert.StartsWith("ENC(", encrypted);
            Assert.EndsWith(")", encrypted);
            Assert.True(SecretProtector.IsEncrypted(encrypted));

            // Decrypt
            string decrypted = SecretProtector.Decrypt(encrypted);
            Assert.Equal(plainText, decrypted);
        }

        [Fact]
        public void Decrypt_NonEncryptedString_ReturnsOriginal()
        {
            string plain = "normal_plain_text";
            string result = SecretProtector.Decrypt(plain);
            Assert.Equal(plain, result);
        }

        [Fact]
        public void DecryptConfiguration_ReplacesEncryptedKeysInMemory()
        {
            var builder = WebApplication.CreateBuilder();
            string rawPassword = "DbSuperPassword123!";
            string encryptedToken = SecretProtector.Encrypt(rawPassword);

            // Mock an encrypted setting
            builder.Configuration["ConnectionStrings:TestDb"] = encryptedToken;
            builder.Configuration["eCourts:ApiKey"] = "regular-unencrypted-key";

            // Act
            builder.Configuration.DecryptConfiguration();

            // Assert
            Assert.Equal(rawPassword, builder.Configuration["ConnectionStrings:TestDb"]);
            Assert.Equal("regular-unencrypted-key", builder.Configuration["eCourts:ApiKey"]);
        }

        [Fact]
        public void EncryptAndVerify_AppSettingsJson_ProtectsAllSecrets()
        {
            string testDir = AppDomain.CurrentDomain.BaseDirectory;
            string appSettingsPath = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", "CaseManagement", "appsettings.json"));
            Assert.True(File.Exists(appSettingsPath), $"Target file not found at {appSettingsPath}");

            string json = File.ReadAllText(appSettingsPath);
            var root = System.Text.Json.Nodes.JsonNode.Parse(json);
            Assert.NotNull(root);

            // Save unencrypted backup for reference
            string backupPath = appSettingsPath + ".unencrypted.bak";
            if (!File.Exists(backupPath))
            {
                File.WriteAllText(backupPath, json);
            }

            void ProtectNode(System.Text.Json.Nodes.JsonNode? node, string key)
            {
                if (node?[key] != null)
                {
                    string? val = node[key]!.GetValue<string>();
                    if (!string.IsNullOrEmpty(val) && !SecretProtector.IsEncrypted(val))
                    {
                        node[key] = SecretProtector.Encrypt(val);
                    }
                }
            }

            ProtectNode(root["ConnectionStrings"], "MVCCaseDB");
            ProtectNode(root["AppSettings"], "TR18ApiKey");
            ProtectNode(root["SMS"], "Password");
            ProtectNode(root["SMS"], "SecureKey");
            ProtectNode(root["eCourts"], "AuthKey");
            ProtectNode(root["eCourts"], "HmacKey");
            ProtectNode(root["eCourts"]?["MVC"], "ApiKey");
            ProtectNode(root["eCourts"]?["MVC"], "SecretKey");
            ProtectNode(root["eCourts"]?["Labour"], "ApiKey");
            ProtectNode(root["eCourts"]?["Labour"], "SecretKey");
            ProtectNode(root["eCourts"]?["OtherCourts"], "ApiKey");
            ProtectNode(root["eCourts"]?["OtherCourts"], "SecretKey");

            var opt = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(appSettingsPath, root.ToJsonString(opt));

            // Verify that WebApplication loads and seamlessly decrypts all keys in-memory
            var builder = WebApplication.CreateBuilder();
            builder.Configuration.AddJsonFile(appSettingsPath, optional: false, reloadOnChange: false);
            builder.Configuration.DecryptConfiguration();

            Assert.Contains("Password=4c4H_0l8q", builder.Configuration.GetConnectionString("MVCCaseDB"));
            Assert.Equal("nwkrtc@1234", builder.Configuration["SMS:Password"]);
            Assert.Equal("5763de722863865e2691e137691d4333", builder.Configuration["eCourts:MVC:ApiKey"]);
        }
    }
}
