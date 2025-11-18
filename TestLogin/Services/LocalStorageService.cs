using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TestLogin.Models;

namespace TestLogin.Services
{
    public static class LocalStorageService
    {
        private static readonly string AppFolder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TestLogin");
        private static readonly string CredsPath = Path.Combine(AppFolder, "credentials.json");
        private static readonly string TokenPath = Path.Combine(AppFolder, "token.dat");

        // Persist credentials; optional plainPassword will be encrypted using DPAPI
        public static void SaveCredentials(StoredCredentials creds, string? plainPassword = null)
        {
            Directory.CreateDirectory(AppFolder);

            if (!string.IsNullOrEmpty(plainPassword))
            {
                var bytes = Encoding.UTF8.GetBytes(plainPassword);
                var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
                creds.EncryptedPasswordBase64 = Convert.ToBase64String(protectedBytes);
            }

            creds.LastLogin = DateTime.UtcNow;

            var json = JsonSerializer.Serialize(creds, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(CredsPath, json, Encoding.UTF8);
        }

        public static StoredCredentials? LoadCredentials()
        {
            try
            {
                if (!File.Exists(CredsPath))
                    return null;

                var json = File.ReadAllText(CredsPath, Encoding.UTF8);
                var creds = JsonSerializer.Deserialize<StoredCredentials>(json);
                return creds;
            }
            catch
            {
                return null;
            }
        }

        public static string? GetDecryptedPassword(StoredCredentials creds)
        {
            try
            {
                if (creds == null || string.IsNullOrEmpty(creds.EncryptedPasswordBase64))
                    return null;

                var protectedBytes = Convert.FromBase64String(creds.EncryptedPasswordBase64);
                var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return null;
            }
        }

        // Save AuthToken (serialize -> protect -> write)
        public static void SaveToken(AuthToken token)
        {
            try
            {
                Directory.CreateDirectory(AppFolder);
                var json = JsonSerializer.Serialize(token);
                var bytes = Encoding.UTF8.GetBytes(json);
                var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(TokenPath, protectedBytes);
            }
            catch
            {
                // ignore persistence failures
            }
        }

        public static AuthToken? LoadToken()
        {
            try
            {
                if (!File.Exists(TokenPath))
                    return null;

                var protectedBytes = File.ReadAllBytes(TokenPath);
                var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
                var json = Encoding.UTF8.GetString(bytes);
                var token = JsonSerializer.Deserialize<AuthToken>(json);
                return token;
            }
            catch
            {
                return null;
            }
        }

        public static void ClearStoredSecrets()
        {
            try
            {
                if (File.Exists(CredsPath)) File.Delete(CredsPath);
                if (File.Exists(TokenPath)) File.Delete(TokenPath);
            }
            catch { }
        }
    }
}