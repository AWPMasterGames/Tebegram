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
        public static readonly string ThemeDataFile   = Path.Combine(AppDataDir, "theme.data");
        // Выбор сервера: "main" (Adress.txt с GitHub) или "custom" (свой адрес)
        public static readonly string ServerChoiceFile = Path.Combine(AppDataDir, "serverChoice.data");
        // Адрес своего сервера для варианта «Другой» — сохраняется для автовходов
        public static readonly string CustomServerFile = Path.Combine(AppDataDir, "customServer.data");

        public static void EnsureDir() => Directory.CreateDirectory(AppDataDir);
    }
}
