using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Tebegrammmm.Data
{
    public static class ServerData
    {
        // Локальный fallback, если адрес не удалось загрузить. Сервер слушает HTTP на 5000
        // (TLS терминирует devtunnel), поэтому здесь именно http, а не https.
        private const string DefaultAdress = "http://localhost:5000";

        /// <summary>Постоянный туннель DrunkMan (для переключателя в настройках).</summary>
        public const string DrunkManTunnel = "https://d6qdbhpn-5000.euw.devtunnels.ms";

        /// <summary>Режим выбора сервера: "drunkman" — жёстко туннель DrunkMan, "auto" — Adress.txt.</summary>
        public static string ServerChoice { get; private set; } = "auto";

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
            // Читаем сохранённый выбор сервера сразу (для корректной подписи в настройках),
            // применяется он ниже — после проверки локального override
            try
            {
                if (System.IO.File.Exists(AppPaths.ServerChoiceFile))
                    ServerChoice = System.IO.File.ReadAllText(AppPaths.ServerChoiceFile).Trim();
            }
            catch { /* нет выбора — авто */ }

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

            // 2. Выбор пользователя из настроек: жёстко туннель DrunkMan
            if (ServerChoice == "drunkman")
            {
                _ServerAdress = DrunkManTunnel;
                return;
            }

            // 3. Adress.txt на GitHub (несколько путей-кандидатов).
            // Кандидат берётся, только если его сервер ЖИВ (/Test отвечает «HI!»).
            // Раньше побеждал первый успешно СКАЧАННЫЙ адрес: мёртвый туннель в
            // файле «закупоривал» авто-режим, хотя дальше по цепочке лежал рабочий
            // адрес (жалоба «не заходит через Adress.txt»). Если не жив ни один —
            // оставляем первый скачанный (прежнее поведение: пусть индикатор честно
            // покажет «сервер не отвечает», а не молча уйдёт на localhost).
            string firstFetched = null;
            foreach (string url in AdressUrls)
            {
                string adress;
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                    string raw = await _http.GetStringAsync(url, cts.Token).ConfigureAwait(false);
                    adress = raw.Split('\n')[0].Trim().TrimEnd('/');
                }
                catch
                {
                    // этот источник недоступен — пробуем следующий
                    continue;
                }
                if (string.IsNullOrWhiteSpace(adress)) continue;

                firstFetched ??= adress;

                if (await IsAliveAsync(adress).ConfigureAwait(false))
                {
                    _ServerAdress = adress;
                    return;
                }
            }

            if (firstFetched != null) _ServerAdress = firstFetched;
        }

        /// <summary>Быстрая проверка живости кандидата: /Test отвечает «HI!» за 2.5 с.</summary>
        private static async Task<bool> IsAliveAsync(string adress)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2.5));
                string a = await _http.GetStringAsync($"{adress}/Test", cts.Token).ConfigureAwait(false);
                return a.Trim() == "HI!";
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Переключение сервера из настроек: "drunkman" — жёстко туннель DrunkMan,
        /// "auto" — обычная цепочка (Adress.txt). Применяется сразу и сохраняется.
        /// </summary>
        public static void SetServerChoice(string choice)
        {
            ServerChoice = choice == "drunkman" ? "drunkman" : "auto";
            try
            {
                AppPaths.EnsureDir();
                System.IO.File.WriteAllText(AppPaths.ServerChoiceFile, ServerChoice);
            }
            catch { /* не сохранился выбор — применим хотя бы на эту сессию */ }

            if (ServerChoice == "drunkman")
                _ServerAdress = DrunkManTunnel;
            else
                GetServerAdress(); // перечитать Adress.txt в фоне
        }

        /// <summary>
        /// Проверяет доступность сервера: сначала дожидается загрузки адреса,
        /// затем запрашивает /Test с таймаутом 3 секунды.
        /// Возвращает true, если сервер ответил «HI!» вовремя.
        /// </summary>
        public static async Task<bool> PingAsync()
        {
            try { await Ready.ConfigureAwait(true); }
            catch { /* адрес мог не загрузиться — всё равно пробуем текущий */ }

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                string a = await _http.GetStringAsync($"{_ServerAdress}/Test", cts.Token).ConfigureAwait(true);
                return a.Trim() == "HI!";
            }
            catch
            {
                return false;
            }
        }
    }
}
