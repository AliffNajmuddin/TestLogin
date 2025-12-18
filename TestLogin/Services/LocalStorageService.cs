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
        private static readonly string SettingsPath = Path.Combine(AppFolder, "settings.json");

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

            // Preserve original token in-memory so we can restore it after serialization
            var originalToken = creds.Token;

            // If a token object is present, encrypt it and clear the plaintext Token so it is not written in cleartext
            if (originalToken != null)
            {
                try
                {
                    var tokenJson = JsonSerializer.Serialize(originalToken);
                    var tokenBytes = Encoding.UTF8.GetBytes(tokenJson);
                    var protectedToken = ProtectedData.Protect(tokenBytes, null, DataProtectionScope.CurrentUser);
                    creds.EncryptedTokenBase64 = Convert.ToBase64String(protectedToken);

                    // Remove plaintext token before writing to disk
                    creds.Token = null;
                }
                catch
                {
                    // If protection fails, do not write plaintext token to disk.
                    creds.EncryptedTokenBase64 = null;
                    creds.Token = null;
                }
            }

            creds.LastLogin = DateTime.UtcNow;

            var json = JsonSerializer.Serialize(creds, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(CredsPath, json, Encoding.UTF8);

            // Restore the in-memory Token so callers keep using the token after SaveCredentials
            creds.Token = originalToken;
        }

        public static StoredCredentials? LoadCredentials()
        {
            try
            {
                if (!File.Exists(CredsPath))
                    return null;

                var json = File.ReadAllText(CredsPath, Encoding.UTF8);
                var creds = JsonSerializer.Deserialize<StoredCredentials>(json);

                if (creds == null)
                    return null;

                // If an encrypted token blob exists, decrypt and populate Token
                if (!string.IsNullOrEmpty(creds.EncryptedTokenBase64))
                {
                    try
                    {
                        var protectedBytes = Convert.FromBase64String(creds.EncryptedTokenBase64);
                        var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
                        var tokenJson = Encoding.UTF8.GetString(bytes);
                        creds.Token = JsonSerializer.Deserialize<AuthToken>(tokenJson);
                    }
                    catch
                    {
                        creds.Token = null;
                    }
                }
                else if (creds.Token != null)
                {
                    // Migration: legacy file contained a plaintext Token property.
                    // Encrypt it and write back to disk so future reads use the encrypted blob.
                    try
                    {
                        // SaveCredentials will encrypt creds.Token and persist EncryptedTokenBase64.
                        // It restores the in-memory Token after writing, so we can call it safely.
                        SaveCredentials(creds, plainPassword: null);
                    }
                    catch
                    {
                        // If migration fails, leave creds.Token as-is in-memory but don't throw.
                    }
                }

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

        // Persist application UI/settings
        public static void SaveSettings(AppSettings settings)
        {
            try
            {
                Directory.CreateDirectory(AppFolder);
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json, Encoding.UTF8);
            }
            catch
            {
                // ignore persistence failures
            }
        }

        public static AppSettings? LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                    return null;

                var json = File.ReadAllText(SettingsPath, Encoding.UTF8);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                return settings;
            }
            catch
            {
                return null;
            }
        }
    }
}