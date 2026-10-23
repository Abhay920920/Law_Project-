using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using MVCCaseManagement.Utils.Security;

namespace Scripts
{
    public class EncryptAppSettings
    {
        public static void Main(string[] args)
        {
            string appSettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Law Project", "CaseManagement", "appsettings.json");
            if (!File.Exists(appSettingsPath))
            {
                // Fallback to relative path
                appSettingsPath = Path.GetFullPath("Law Project/CaseManagement/appsettings.json");
            }

            Console.WriteLine($"[EncryptAppSettings] Reading {appSettingsPath}...");
            string json = File.ReadAllText(appSettingsPath);
            var root = JsonNode.Parse(json);
            if (root == null)
            {
                Console.WriteLine("Error: Could not parse appsettings.json");
                return;
            }

            // Keys to protect
            ProtectProperty(root, "ConnectionStrings", "MVCCaseDB");
            ProtectProperty(root, "AppSettings", "TR18ApiKey");
            ProtectProperty(root, "SMS", "Password");
            ProtectProperty(root, "SMS", "SecureKey");
            ProtectProperty(root, "eCourts", "AuthKey");
            ProtectProperty(root, "eCourts", "HmacKey");
            ProtectNestedProperty(root, "eCourts", "MVC", "ApiKey");
            ProtectNestedProperty(root, "eCourts", "MVC", "SecretKey");
            ProtectNestedProperty(root, "eCourts", "Labour", "ApiKey");
            ProtectNestedProperty(root, "eCourts", "Labour", "SecretKey");
            ProtectNestedProperty(root, "eCourts", "OtherCourts", "ApiKey");
            ProtectNestedProperty(root, "eCourts", "OtherCourts", "SecretKey");

            // Write backup
            string backupPath = appSettingsPath + ".backup";
            if (!File.Exists(backupPath))
            {
                File.WriteAllText(backupPath, json);
                Console.WriteLine($"[EncryptAppSettings] Backup created at: {backupPath}");
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            string encryptedJson = root.ToJsonString(options);
            File.WriteAllText(appSettingsPath, encryptedJson);
            Console.WriteLine($"[EncryptAppSettings] Successfully encrypted all sensitive credentials in {appSettingsPath}!");
        }

        private static void ProtectProperty(JsonNode root, string section, string propName)
        {
            var secNode = root[section];
            if (secNode != null && secNode[propName] != null)
            {
                string? raw = secNode[propName]!.GetValue<string>();
                if (!string.IsNullOrEmpty(raw) && !SecretProtector.IsEncrypted(raw))
                {
                    string enc = SecretProtector.Encrypt(raw);
                    secNode[propName] = enc;
                    Console.WriteLine($"  [PROTECTED] {section}:{propName}");
                }
            }
        }

        private static void ProtectNestedProperty(JsonNode root, string section, string subSection, string propName)
        {
            var secNode = root[section]?[subSection];
            if (secNode != null && secNode[propName] != null)
            {
                string? raw = secNode[propName]!.GetValue<string>();
                if (!string.IsNullOrEmpty(raw) && !SecretProtector.IsEncrypted(raw))
                {
                    string enc = SecretProtector.Encrypt(raw);
                    secNode[propName] = enc;
                    Console.WriteLine($"  [PROTECTED] {section}:{subSection}:{propName}");
                }
            }
        }
    }
}
