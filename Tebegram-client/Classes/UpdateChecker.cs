using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

namespace Tebegrammmm.Classes
{
    /// <summary>
    /// Проверка обновлений: сравнивает свою версию с version.txt в репозитории.
    /// Если на GitHub версия новее — предлагает открыть страницу загрузки установщика.
    /// Чтобы выпустить обновление: поднять версию здесь и в Installer/TebegramSetup.iss,
    /// собрать установщик, выложить его в GitHub Releases и поднять версию в version.txt (ветка main).
    /// </summary>
    public static class UpdateChecker
    {
        // Текущая версия клиента. Должна совпадать с MyAppVersion в Installer/TebegramSetup.iss
        public const string CurrentVersion = "1.0.5";

        // Кандидаты в порядке приоритета: main — основной источник для пользователей,
        // main-dev-Test — запасной (в main файла version.txt пока нет, там всегда 404)
        private static readonly string[] VersionUrls =
        {
            "https://raw.githubusercontent.com/AWPMasterGames/Tebegram/refs/heads/main/version.txt",
            "https://raw.githubusercontent.com/AWPMasterGames/Tebegram/refs/heads/main-dev-Test/version.txt",
        };
        private const string DownloadPageUrl = "https://github.com/AWPMasterGames/Tebegram/releases/latest";

        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        /// <summary>
        /// Проверка обновлений. По умолчанию тихая (для запуска приложения);
        /// с notifyIfLatest=true сообщает результат в любом случае (для кнопки в настройках).
        /// </summary>
        public static async Task CheckAsync(bool notifyIfLatest = false)
        {
            try
            {
                string raw = null;
                foreach (string url in VersionUrls)
                {
                    try
                    {
                        raw = (await _http.GetStringAsync(url)).Split('\n')[0].Trim();
                        break;
                    }
                    catch
                    {
                        // этот источник недоступен (например, 404 в main) — пробуем следующий
                    }
                }

                if (raw == null ||
                    !Version.TryParse(raw, out Version latest) ||
                    !Version.TryParse(CurrentVersion, out Version current))
                {
                    if (notifyIfLatest) TbgDialogWindow.Show("Не удалось проверить обновления.", "Обновление Tebegram");
                    return;
                }
                if (latest <= current)
                {
                    if (notifyIfLatest) TbgDialogWindow.Show($"У вас последняя версия ({current}).", "Обновление Tebegram");
                    return;
                }

                bool open = TbgDialogWindow.Confirm(
                    $"Доступна новая версия Tebegram {latest} (у вас {current}).\n\nОткрыть страницу загрузки?",
                    "Обновление Tebegram",
                    "Открыть", "Позже");

                if (open)
                {
                    Process.Start(new ProcessStartInfo(DownloadPageUrl) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                // Нет сети или GitHub недоступен — тихая проверка просто пропускается
                Log.Save($"[UpdateChecker] {ex.GetType().Name}: {ex.Message}");
                if (notifyIfLatest) TbgDialogWindow.Show("Не удалось проверить обновления. Проверь подключение к интернету.", "Обновление Tebegram");
            }
        }
    }
}
