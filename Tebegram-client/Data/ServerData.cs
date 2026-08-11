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

        /// <summary>
        /// Выбор сервера: "main" - адрес из Adress.txt на GitHub (по умолчанию, при
        /// первом входе), "custom" - адрес, введённый пользователем (пункт «Другой»).
        /// Хранится в serverChoice.data, сам адрес «Другого» - в customServer.data,
        /// поэтому выбор переживает перезапуски (для автоматических входов).
        /// </summary>
        public static string ServerChoice { get; private set; } = "main";

        /// <summary>Адрес своего сервера (вариант «Другой»); пуст, если не задан.</summary>
        public static string CustomAdress { get; private set; } = "";

        // Источники адреса сервера в порядке приоритета. Все указывают на ветку
        // main: по ней работают выпущенные сборки, и рабочая ветка не должна
        // уводить их на временный адрес.
        //
        // Adress.txt в корне репозитория - канонический файл, ведётся вручную.
        //
        // AlternativeAdress.txt - второй сервер команды. Очередь доходит до него,
        // только когда основной не отвечает на /Test: берётся первый ЖИВОЙ источник,
        // а не первый скачанный. Переключение выходит автоматическим.
        //
        // Tebegram-client/Adress.txt - зеркало для установленных клиентов 2.0.0,
        // в цепочке которых корневого пути ещё нет. Пока такие клиенты используются,
        // адрес в обоих файлах должен совпадать; затем зеркало удаляется.
        //
        // Ветка main-dev-Test - запасной вариант. Недоступный источник пропускается,
        // поэтому лишние записи в списке безопасны.
        private static readonly string[] AdressUrls =
        {
            "https://raw.githubusercontent.com/AWPMasterGames/Tebegram/refs/heads/main/Adress.txt",
            "https://raw.githubusercontent.com/AWPMasterGames/Tebegram/refs/heads/main/AlternativeAdress.txt",
            "https://raw.githubusercontent.com/AWPMasterGames/Tebegram/refs/heads/main/Tebegram-client/Adress.txt",
            "https://raw.githubusercontent.com/AWPMasterGames/Tebegram/refs/heads/main-dev-Test/Tebegram-client/Adress.txt",
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
            LoadChoice();

            // Вариант «Другой»: используем адрес, введённый пользователем, как есть.
            // Пустой custom не должен обрубать вход - тогда падаем на цепочку main.
            if (ServerChoice == "custom" && !string.IsNullOrWhiteSpace(CustomAdress))
            {
                _ServerAdress = CustomAdress.TrimEnd('/');
                return;
            }

            // Вариант «main» всегда берёт адрес из Adress.txt на GitHub и не должен
            // сохранять адрес, введённый в варианте «Другой». Ранее при переключении
            // с собственного адреса на main и недоступном GitHub поле _ServerAdress
            // сохраняло прежнее значение, и клиент с отметкой «main» продолжал
            // обращаться к личному серверу.
            //
            // Поэтому значение сбрасывается на localhost: если ни один источник не
            // загрузится, индикатор покажет отсутствие связи, а не посторонний адрес.
            _ServerAdress = DefaultAdress;

            // Кандидат берётся, только если его сервер ЖИВ (/Test отвечает «HI!»).
            // Первый успешно СКАЧАННЫЙ адрес запоминаем как запасной: мёртвый туннель
            // в файле не должен «закупоривать» цепочку, если дальше лежит рабочий адрес.
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
                    // этот источник недоступен - пробуем следующий
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
        /// Читает сохранённый выбор сервера и адрес «Другого» из файлов.
        /// Любое старое/незнакомое значение (в т.ч. прежние "auto"/"drunkman")
        /// приводим к "main" - новых вариантов только два.
        /// </summary>
        private static void LoadChoice()
        {
            try
            {
                if (System.IO.File.Exists(AppPaths.ServerChoiceFile))
                    ServerChoice = System.IO.File.ReadAllText(AppPaths.ServerChoiceFile).Trim();
            }
            catch { /* нет файла - main */ }
            if (ServerChoice != "custom") ServerChoice = "main";

            try
            {
                if (System.IO.File.Exists(AppPaths.CustomServerFile))
                    CustomAdress = System.IO.File.ReadAllText(AppPaths.CustomServerFile)
                        .Split('\n')[0].Trim().TrimEnd('/');
            }
            catch { /* нет своего адреса - пусто */ }
        }

        /// <summary>
        /// Меняет выбор сервера и СОХРАНЯЕТ его для следующих автовходов.
        /// choice = "main" - цепочка Adress.txt; "custom" - адрес customAdress
        /// (если передан - запоминается). Применяется сразу.
        /// </summary>
        public static void SetServerChoice(string choice, string customAdress = null)
        {
            if (choice == "custom")
            {
                ServerChoice = "custom";
                if (customAdress != null) CustomAdress = customAdress.Trim().TrimEnd('/');
                Save();
                _ServerAdress = string.IsNullOrWhiteSpace(CustomAdress) ? DefaultAdress : CustomAdress;
            }
            else
            {
                ServerChoice = "main";
                Save();
                GetServerAdress(); // перечитать Adress.txt в фоне
            }
        }

        private static void Save()
        {
            try
            {
                AppPaths.EnsureDir();
                System.IO.File.WriteAllText(AppPaths.ServerChoiceFile, ServerChoice);
                System.IO.File.WriteAllText(AppPaths.CustomServerFile, CustomAdress ?? "");
            }
            catch { /* не сохранилось - применим хотя бы на эту сессию */ }
        }

        /// <summary>
        /// Проверяет доступность сервера: сначала дожидается загрузки адреса,
        /// затем запрашивает /Test с таймаутом 3 секунды.
        /// Возвращает true, если сервер ответил «HI!» вовремя.
        /// </summary>
        public static async Task<bool> PingAsync()
        {
            try { await Ready.ConfigureAwait(true); }
            catch { /* адрес мог не загрузиться - всё равно пробуем текущий */ }

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
