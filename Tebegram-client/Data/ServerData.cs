using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Tebegrammmm.Data
{
    public static class ServerData
    {
        private const string DefaultAdress = "http://localhost:5000";
        private const string AdressUrl = "https://raw.githubusercontent.com/AWPMasterGames/Tebegram/refs/heads/main/Tebegrammmm/Adress.txt";

        private static string _ServerAdress = DefaultAdress;
        public static string ServerAdress { get { return _ServerAdress; } }

        public static bool IsConnected { get; private set; } = false;

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
            // Timeout не выставляем — каждый запрос управляет своим CancellationTokenSource
            return new HttpClient(handler) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
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
            catch { /* GitHub недоступен — остаёмся на localhost */ }

            await CheckAdressValidAsync().ConfigureAwait(false);
        }

        // Проверяет адрес через /Test. Если сервер не отвечает "HI!" — откат на localhost.
        public static async Task CheckAdressValidAsync()
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                string a = await _http.GetStringAsync($"{_ServerAdress}/Test", cts.Token).ConfigureAwait(false);
                if (a.Trim() == "HI!")
                {
                    IsConnected = true;
                    return;
                }
            }
            catch { }

            // Адрес не прошёл проверку (недоступен или вернул не "HI!") — откат на localhost
            IsConnected = false;
            _ServerAdress = DefaultAdress;
        }
    }
}
