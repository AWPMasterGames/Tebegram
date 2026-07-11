namespace TebegramServer.Classes
{
    public static class Logs
    {
        private static readonly object _lock = new object();

        public static void Save(string log)
        {
            // Логирование не должно ронять запросы: раньше File.Create оставлял файл
            // ОТКРЫТЫМ (незакрытый FileStream), первый же AppendAllText падал
            // IOException «файл занят», а /login, логируя ПОСЛЕ отправки ответа,
            // обрывал соединение — у клиента это выглядело как «ошибка авторизации».
            try
            {
                DateTime dateTime = DateTime.Now;
                string dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
                Directory.CreateDirectory(dir); // идемпотентно, создаёт при отсутствии

                string path = Path.Combine(dir, $"{dateTime:dd.MM.yyyy}.txt");
                lock (_lock) // параллельные запросы не должны драться за файл
                {
                    // AppendAllText сам создаёт файл, File.Create не нужен
                    File.AppendAllText(path, $"[{dateTime:dd.MM.yyyy HH:mm:ss}]  {log}\n");
                }
            }
            catch
            {
                // не смогли записать лог — молча пропускаем, запрос важнее
            }
        }
    }
}
