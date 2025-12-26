namespace TebegramServer.Classes
{
    public static class Logs
    {
        public static void Save(string log)
        {
            DateTime dateTime = DateTime.Now;
            if (!Directory.Exists($"{Directory.GetCurrentDirectory()}/Logs"))
            {
                Directory.CreateDirectory($"{Directory.GetCurrentDirectory()}/Logs");
            }
            else if (!File.Exists($"{Directory.GetCurrentDirectory()}/Logs/{dateTime.ToString("dd.MM.yyyy")}.txt"))
            {
                File.Create($"{Directory.GetCurrentDirectory()}/Logs/{dateTime.ToString("dd.MM.yyyy")}.txt");
            }
            File.AppendAllText($"{Directory.GetCurrentDirectory()}/Logs/{dateTime.ToString("dd.MM.yyyy")}.txt",$"[{dateTime.ToString("dd.MM.yyyy HH:mm:ss")}]  {log}\n");
        }
    }
}
