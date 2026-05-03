using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Tebegrammmm.Data
{
    public static class AppPaths
    {
        public static readonly string AppDataDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tebegram");

        public static readonly string UserDataFile    = Path.Combine(AppDataDir, "user.data");
        public static readonly string DeviceDataFile  = Path.Combine(AppDataDir, "userDevice.data");
        public static readonly string LastChatFile    = Path.Combine(AppDataDir, "lastchat.data");
        public static readonly string ThemeDataFile   = Path.Combine(AppDataDir, "theme.data");
        public static readonly string AvatarCacheDir  = Path.Combine(AppDataDir, "avatars");

        public static void EnsureDir()
        {
            Directory.CreateDirectory(AppDataDir);
            Directory.CreateDirectory(AvatarCacheDir);
        }

        /// <summary>
        /// Путь к кэш-файлу: %LOCALAPPDATA%/Tebegram/avatars/{md5(url)}{ext}
        /// </summary>
        public static string GetAvatarCachePath(string url)
        {
            byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(url));
            string hashStr = Convert.ToHexString(hash).ToLowerInvariant();

            string ext = string.Empty;
            try { ext = Path.GetExtension(new Uri(url).LocalPath); } catch { }
            if (string.IsNullOrEmpty(ext)) ext = ".jpg";

            EnsureDir();
            return Path.Combine(AvatarCacheDir, hashStr + ext);
        }
    }
}
