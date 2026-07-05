using System;
using System.IO;
using System.Threading;

namespace Tebegrammmm.Classes
{
    static class Log
    {
        private static string _CrashLogsDirectory;
        private static object _lock = new object();
        private static bool CheckDirectoryes()
        {
            _CrashLogsDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Tebegram", "CrashLogs");
            if (!Directory.Exists(_CrashLogsDirectory))
            {
                Directory.CreateDirectory(_CrashLogsDirectory);
            }
            return true;
        }

        public static void Save(string log)
        {
            DateTime dateTime = DateTime.Now;
            CheckDirectoryes();
            lock (_lock)
            {
                // AppendAllText сам создаёт файл; Thread.Sleep(100) здесь тормозил каждый вызов лога
                File.AppendAllText($"{_CrashLogsDirectory}/{dateTime.ToString("dd.MM.yyyy")}.txt", $"[{dateTime.ToString("dd.MM.yyyy HH:mm:ss")}]  {log}\n");
            }
        }
    }
}
