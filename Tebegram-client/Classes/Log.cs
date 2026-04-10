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
            lock (_lock)
            {

                if (!File.Exists($"{_CrashLogsDirectory}/{dateTime.ToString("dd.MM.yyyy")}.txt"))
                {
                    File.Create($"{_CrashLogsDirectory}/{dateTime.ToString("dd.MM.yyyy")}.txt").Close();
                }
                Thread.Sleep( 100 );
                File.AppendAllText($"{_CrashLogsDirectory}/{dateTime.ToString("dd.MM.yyyy")}.txt", $"[{dateTime.ToString("dd.MM.yyyy HH:mm:ss")}]  {log}\n");
            }
        }
    }
}
