namespace TebegramServer.Classes
{
    public static class Logs
    {
        public static void Save(string log)
        {
            DateTime dateTime = DateTime.Now;
            string dir = $"{Directory.GetCurrentDirectory()}/Logs";
            Directory.CreateDirectory(dir);
            File.AppendAllText($"{dir}/{dateTime:dd.MM.yyyy}.txt", $"[{dateTime:dd.MM.yyyy HH:mm:ss}]  {log}\n");
        }
    }
}
