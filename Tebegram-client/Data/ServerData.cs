using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Tebegrammmm.Data
{
    public static class ServerData
    {
#if MAX
        private const string AdressUrl = "https://raw.githubusercontent.com/AWPMasterGames/Tebegram/refs/heads/main/Tebegrammmm/Adress.txt";
#elif SERGE
        private const string AdressUrl = "https://raw.githubusercontent.com/DrunkMan404/ServerAddres/main/Adress.txt";
#endif

        private static string _ServerAdress = string.Empty;
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
            return new HttpClient(handler) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
        }

        /// <summary>
        /// Запускает фоновую загрузку адреса сервера. Не блокирует UI-поток.
        /// </summary>
        public static void GetServerAdress()
        {
            _readyTask = Task.Run(RefreshAdressAsync);
        }

        /// <summary>
        /// Повторно проверяет соединение. Используется перед запросами, если IsConnected == false.
        /// </summary>
        public static Task RetryAsync()
        {
            _readyTask = Task.Run(RefreshAdressAsync);
            return _readyTask;
        }

        private static async Task RefreshAdressAsync()
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                string raw = await _http.GetStringAsync(AdressUrl, cts.Token).ConfigureAwait(false);
                string adress = raw.Split('\n')[0].Trim().TrimEnd('/');
                if (!string.IsNullOrWhiteSpace(adress))
                    _ServerAdress = adress;
            }
            catch { }

            await CheckAdressValidAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Проверяет доступность сервера через /Test.
        /// </summary>
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

            IsConnected = false;
        }
    }
}
