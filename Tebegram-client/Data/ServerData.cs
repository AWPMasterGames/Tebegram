using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Tebegrammmm.Data
{
    public static class ServerData
    {
        private const string DefaultAdress = "https://localhost:5000";
        private const string AdressUrl = "https://raw.githubusercontent.com/AWPMasterGames/Tebegram/refs/heads/main/Tebegrammmm/Adress.txt";

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
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                string raw = await _http.GetStringAsync(AdressUrl, cts.Token).ConfigureAwait(false);
                string adress = raw.Split('\n')[0].Trim();
                if (!string.IsNullOrWhiteSpace(adress))
                    _ServerAdress = adress;
            }
            catch
            {
                // нет сети / GitHub недоступен — остаёмся на DefaultAdress
            }

            await CheckAdressValidAsync().ConfigureAwait(false);
        }

        public static async Task CheckAdressValidAsync()
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                string a = await _http.GetStringAsync($"{_ServerAdress}/Test", cts.Token).ConfigureAwait(false);
                if (a == "Hi!") return;
            }
            catch
            {
                // адрес недоступен — не подменяем, чтобы не нарушить ранее работавший сценарий
            }
        }
    }
}
