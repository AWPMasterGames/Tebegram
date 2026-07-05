using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Tebegrammmm.Data
{
    public static class ServerData
    {
        private const string DefaultAdress = "https://localhost:5000";

        /// <summary>
        /// Файл рядом с exe для ручного переопределения адреса (для локальных тестов).
        /// В установщик не попадает (CopyToPublishDirectory=Never) — у пользователей
        /// работает обычный механизм с Adress.txt на GitHub.
        /// </summary>
        private const string OverrideFileName = "Adress.override.txt";

        // Источники адреса сервера в порядке приоритета.
        // Первый — ветка main-dev-Test (ВРЕМЕННО, для проверки туннеля DrunkMan),
        // дальше — основные пути в main (новая и старая раскладка репозитория).
        private static readonly string[] AdressUrls =
        {
            "https://raw.githubusercontent.com/AWPMasterGames/Tebegram/refs/heads/main-dev-Test/Tebegram-client/Adress.txt",
            "https://raw.githubusercontent.com/AWPMasterGames/Tebegram/refs/heads/main/Tebegram-client/Adress.txt",
            "https://raw.githubusercontent.com/AWPMasterGames/Tebegram/refs/heads/main/Tebegrammmm/Adress.txt",
        };

        private static string _ServerAdress = DefaultAdress;
        public static string ServerAdress { get { return _ServerAdress; } }

        private static readonly HttpClient _http = CreateHttpClient();
        private static Task _readyTask = Task.CompletedTask;

        /// <summary>
        /// Задача, завершающаяся когда адрес сервера загружен (или истёк таймаут).
        /// Вызывающий код может await-ить её перед обращением к серверу.
        /// </summary>
        public static Task Ready => _readyTask;

        private static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (m, c, ch, e) => true
            };
            return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(4) };
        }

        /// <summary>
        /// Запускает фоновую загрузку адреса сервера. Не блокирует UI-поток.
        /// Адрес не сохраняется на диск.
        /// </summary>
        public static void GetServerAdress()
        {
            _readyTask = Task.Run(RefreshAdressAsync);
        }

        private static async Task RefreshAdressAsync()
        {
            // 1. Локальный override рядом с exe — высший приоритет (для тестов)
            try
            {
                string overridePath = System.IO.Path.Combine(AppContext.BaseDirectory, OverrideFileName);
                if (System.IO.File.Exists(overridePath))
                {
                    string overrideAdress = System.IO.File.ReadAllText(overridePath).Split('\n')[0].Trim();
                    if (!string.IsNullOrWhiteSpace(overrideAdress))
                    {
                        _ServerAdress = overrideAdress.TrimEnd('/');
                        return;
                    }
                }
            }
            catch
            {
                // не смогли прочитать override — идём обычным путём
            }

            // 2. Adress.txt на GitHub (несколько путей-кандидатов)
            foreach (string url in AdressUrls)
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                    string raw = await _http.GetStringAsync(url, cts.Token).ConfigureAwait(false);
                    string adress = raw.Split('\n')[0].Trim();
                    if (!string.IsNullOrWhiteSpace(adress))
                    {
                        _ServerAdress = adress.TrimEnd('/');
                        break;
                    }
                }
                catch
                {
                    // этот источник недоступен — пробуем следующий
                }
            }

            await CheckAdressValidAsync().ConfigureAwait(false);
        }

        public static async Task CheckAdressValidAsync()
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                string a = await _http.GetStringAsync($"{_ServerAdress}/Test", cts.Token).ConfigureAwait(false);
                if (a == "HI!") return;
            }
            catch
            {
                // адрес недоступен — не подменяем, чтобы не нарушить ранее работавший сценарий
            }
        }
    }
}
