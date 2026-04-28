using System;
using System.IO;

namespace Tebegrammmm.Data
{
    public static class AppPaths
    {
        public static readonly string AppDataDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tebegram");

        public static readonly string UserDataFile    = Path.Combine(AppDataDir, "user.data");
        public static readonly string DeviceDataFile  = Path.Combine(AppDataDir, "userDevice.data");

        public static void EnsureDir() => Directory.CreateDirectory(AppDataDir);
    }
}
