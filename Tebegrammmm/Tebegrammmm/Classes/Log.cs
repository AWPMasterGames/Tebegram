using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tebegrammmm.Classes
{
    static class Log
    {
        private static string _CrashLogsDirectory;
        private static object _lock = new object();
        private static bool CheckDirectoryes()
        {
            _CrashLogsDirectory = $"{Directory.GetCurrentDirectory()}/CrashLogs";
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

            if (!File.Exists($"{_CrashLogsDirectory}/{dateTime.ToString("dd.MM.yyyy")}.txt"))
            {
                File.Create($"{_CrashLogsDirectory}/{dateTime.ToString("dd.MM.yyyy")}.txt");
            }
            lock (_lock)
            {
                File.AppendAllText($"{_CrashLogsDirectory}/{dateTime.ToString("dd.MM.yyyy")}.txt", $"[{dateTime.ToString("dd.MM.yyyy HH:mm:ss")}]  {log}\n");
            }
        }
    }
}
