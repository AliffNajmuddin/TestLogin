using System;
using System.IO;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using TestLogin.Models;

namespace TestLogin.Services
{
    public static class LocalStorageService
    {
        private static readonly string AppDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TestLogin");

        private static readonly string CredentialsFile = Path.Combine(AppDataPath, "credentials.json");
        private static readonly string SettingsFile = Path.Combine(AppDataPath, "settings.json");
        private static readonly string UsersFile = Path.Combine(AppDataPath, "users.csv");

        // Encryption key (in production, use a more secure key management)
        private static readonly byte[] EncryptionKey = Encoding.UTF8.GetBytes("Your32ByteEncryptionKey123!"); // 32 bytes

        static LocalStorageService()
        {
            // Ensure directory exists
            if (!Directory.Exists(AppDataPath))
            {
                Directory.CreateDirectory(AppDataPath);
            }

            // Ensure a users CSV exists (will not overwrite an existing file)
            EnsureUsersFileExists();
        }

        private static void EnsureUsersFileExists()
        {
            try
            {
                if (File.Exists(UsersFile))
                    return;

                // Default/demo users - only created if file missing
                var lines = new List<string>
                {
                    "Username,Password,FullName,Email,Role",
                    "alice,alice123,Alice Anderson,alice@example.com,User",
                    "bob,bob123,Bob Brown,bob@example.com,User",
                    "charlie,charlie123,Charlie Clark,charlie@example.com,Engineer",
                    "diana,diana123,Diana Diaz,diana@example.com,User",
                    "evan,evan123,Evan Edwards,evan@example.com,Manager"
                };

                File.WriteAllLines(UsersFile, lines);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create users file: {ex.Message}");
            }
        }

        /// <summary>
        /// Load user records from users.csv (simple CSV, no quoted-field support).
        /// </summary>
        public static List<UserRecord> LoadUserRecords()
        {
            var users = new List<UserRecord>();

            try
            {
                if (!File.Exists(UsersFile))
                    return users;

                var lines = File.ReadAllLines(UsersFile);
                if (lines.Length <= 1)
                    return users; // only header or empty

                // Skip header
                foreach (var line in lines.Skip(1))
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var parts = line.Split(',');
                    if (parts.Length >= 5)
                    {
                        users.Add(new UserRecord
                        {
                            Username = parts[0].Trim(),
                            Password = parts[1].Trim(),
                            FullName = parts[2].Trim(),
                            Email = parts[3].Trim(),
                            Role = parts[4].Trim()
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load user records: {ex.Message}");
            }

            return users;
        }

        /// <summary>
        /// Validate credentials against users.csv. Returns a UserInfo on success, otherwise null.
        /// </summary>
        public static UserInfo ValidateCredentials(string username, string password)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(username) || password == null)
                    return null;

                var users = LoadUserRecords();
                var match = users.FirstOrDefault(u =>
                    string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase) &&
                    u.Password == password);

                if (match != null)
                {
                    return new UserInfo
                    {
                        Username = match.Username,
                        Email = match.Email,
                        FullName = match.FullName,
                        Role = match.Role
                    };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Credential validation failed: {ex.Message}");
            }

            return null;
        }

        public static void SaveCredentials(StoredCredentials credentials)
        {
            try
            {
                // Encrypt password before saving
                if (!string.IsNullOrEmpty(credentials.EncryptedPassword))
                {
                    credentials.EncryptedPassword = Encrypt(credentials.EncryptedPassword);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(credentials, options);
                File.WriteAllText(CredentialsFile, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save credentials: {ex.Message}", "Storage Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public static StoredCredentials LoadCredentials()
        {
            try
            {
                if (!File.Exists(CredentialsFile))
                    return null;

                string json = File.ReadAllText(CredentialsFile);
                var credentials = JsonSerializer.Deserialize<StoredCredentials>(json);

                // Decrypt password after loading
                if (!string.IsNullOrEmpty(credentials?.EncryptedPassword))
                {
                    credentials.EncryptedPassword = Decrypt(credentials.EncryptedPassword);
                }

                return credentials;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load credentials: {ex.Message}", "Storage Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }
        }

        public static void ClearCredentials()
        {
            try
            {
                if (File.Exists(CredentialsFile))
                {
                    File.Delete(CredentialsFile);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to clear credentials: {ex.Message}", "Storage Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Simple encryption (for demo purposes - use more secure methods in production)
        private static string Encrypt(string plainText)
        {
            try
            {
                using var aes = Aes.Create();
                aes.Key = EncryptionKey;
                aes.GenerateIV();

                using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                using var ms = new MemoryStream();

                // Write IV first
                ms.Write(aes.IV, 0, aes.IV.Length);

                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                using (var sw = new StreamWriter(cs))
                {
                    sw.Write(plainText);
                }

                return Convert.ToBase64String(ms.ToArray());
            }
            catch
            {
                return plainText; // Fallback to plain text if encryption fails
            }
        }

        private static string Decrypt(string cipherText)
        {
            try
            {
                var fullCipher = Convert.FromBase64String(cipherText);

                using var aes = Aes.Create();
                aes.Key = EncryptionKey;

                // Extract IV from the beginning of the stream
                var iv = new byte[16];
                Array.Copy(fullCipher, 0, iv, 0, iv.Length);
                aes.IV = iv;

                using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                using var ms = new MemoryStream(fullCipher, iv.Length, fullCipher.Length - iv.Length);
                using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                using var sr = new StreamReader(cs);

                return sr.ReadToEnd();
            }
            catch
            {
                return cipherText; // Fallback if decryption fails
            }
        }

        public static void SaveSettings(AppSettings settings)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(SettingsFile, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }

        public static AppSettings LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsFile))
                    return new AppSettings(); // Return default settings

                string json = File.ReadAllText(SettingsFile);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
                return new AppSettings(); // Return default settings
            }
        }
    }
}