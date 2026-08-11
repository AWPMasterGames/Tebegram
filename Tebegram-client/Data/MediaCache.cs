using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Tebegrammmm.Classes;

namespace Tebegrammmm.Data
{
    /// <summary>
    /// Локальный кэш медиа. Показанное или открытое фото и видео остаётся на диске
    /// в %LOCALAPPDATA%\Tebegram\cache и повторно с сервера не запрашивается.
    ///
    /// Устройство кэша повторяет схему Telegram Desktop, но без шифрования: там
    /// кэш закрыт ключом от облачного пароля, здесь такого ключа нет.
    ///
    /// Принятые решения:
    /// files хранит оригиналы, preview - уменьшенные картинки и кадры видео.
    /// Два уровня нужны потому, что списку сообщений хватает превью: оно быстрее
    /// декодируется и занимает меньше памяти, а оригинал требуется просмотрщику.
    ///
    /// Ключ записи - имя файла на сервере, а не полный URL. Адрес сервера меняется
    /// при каждом перезапуске туннеля, тогда как имя в /upload/ постоянно и уникально.
    ///
    /// Объём ограничен сверху, при переполнении вытесняются самые старые записи.
    /// </summary>
    public static class MediaCache
    {
        public static readonly string Root = Path.Combine(AppPaths.AppDataDir, "cache");
        /// <summary>Оригиналы файлов (то, что скачано с /upload/).</summary>
        public static readonly string FilesDir = Path.Combine(Root, "files");
        /// <summary>Уменьшенные картинки: превью фото и первый кадр видео.</summary>
        public static readonly string PreviewDir = Path.Combine(Root, "preview");

        /// <summary>
        /// Потолок кэша оригиналов; всё сверх него вытесняется. Не константа - 
        /// значение задаётся снаружи (настройки, тесты), как «Управление памятью»
        /// в Telegram.
        /// </summary>
        public static long MaxBytes { get; set; } = 512L * 1024 * 1024;

        /// <summary>Ширина, до которой ужимается превью для пузыря (в пузыре 320 DIP, с запасом на 200% DPI).</summary>
        public const int PreviewWidth = 640;

        // Сертификат сервера самоподписанный - как и в остальных клиентских запросах.
        // Таймаут большой: по туннелю видео качается долго, а обрыв на середине
        // означал бы, что файл так и не попадёт в кэш.
        private static readonly HttpClient _http = new(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (m, c, ch, e) => true
        })
        { Timeout = TimeSpan.FromMinutes(10) };

        // Один и тот же файл могут запросить одновременно пузырь, просмотрщик и
        // предзагрузка. Держим ОДНУ задачу на файл, иначе он качается в три потока.
        private static readonly ConcurrentDictionary<string, Task<string>> _inFlight = new();

        // Предзагрузка не должна отбирать канал у того, что пользователь смотрит
        // прямо сейчас: не больше двух фоновых загрузок разом.
        private static readonly SemaphoreSlim _prefetchGate = new(2);

        // ── Ключи и пути ─────────────────────────────────────────────────────

        /// <summary>
        /// Имя файла в кэше: хэш от имени файла на сервере + исходное расширение.
        /// Хэш - чтобы кириллица, пробелы и длинные имена не ломали путь;
        /// расширение сохраняем, потому что MediaElement выбирает декодер по нему.
        /// </summary>
        private static string KeyFor(string url)
        {
            string name = ServerFileName(url);
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name)))
                                 .Substring(0, 24).ToLowerInvariant();

            string ext = Path.GetExtension(name);
            // В расширение пускаем только буквы и цифры: имя приходит из сети
            if (ext.Length > 1 && ext.Length <= 8 && ext.Skip(1).All(char.IsLetterOrDigit))
                return hash + ext.ToLowerInvariant();
            return hash;
        }

        /// <summary>Имя файла на сервере (последний сегмент URL, без %-кодирования).</summary>
        private static string ServerFileName(string url)
        {
            try
            {
                var uri = new Uri(url, UriKind.Absolute);
                return Uri.UnescapeDataString(uri.Segments.LastOrDefault() ?? url);
            }
            catch
            {
                return url; // не URL - ключуем как есть, лишь бы стабильно
            }
        }

        private static string FilePathFor(string url) => Path.Combine(FilesDir, KeyFor(url));

        // ── Оригиналы ────────────────────────────────────────────────────────

        /// <summary>
        /// Файл уже лежит на диске? Отвечает мгновенно и без сети - просмотрщик
        /// по этому ответу решает, играть с диска или тянуть с сервера.
        /// </summary>
        public static bool TryGetLocalPath(string url, out string path)
        {
            path = null;
            if (string.IsNullOrEmpty(url)) return false;

            try
            {
                var info = new FileInfo(FilePathFor(url));
                if (!info.Exists || info.Length == 0) return false;

                // Отмечаем использование: по этой метке вытесняются старые записи.
                // Раз в час достаточно - незачем дёргать диск на каждое обращение.
                if (DateTime.UtcNow - info.LastWriteTimeUtc > TimeSpan.FromHours(1))
                {
                    try { File.SetLastWriteTimeUtc(info.FullName, DateTime.UtcNow); }
                    catch { /* файл занят плеером - не страшно */ }
                }

                path = info.FullName;
                return true;
            }
            catch (Exception ex)
            {
                Log.Save($"[MediaCache.TryGetLocalPath] {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Путь к локальной копии файла: из кэша, а при промахе - скачивает.
        /// Возвращает null, если скачать не вышло.
        /// </summary>
        public static Task<string> GetLocalPathAsync(string url)
        {
            if (string.IsNullOrEmpty(url)) return Task.FromResult<string>(null);
            if (TryGetLocalPath(url, out string existing)) return Task.FromResult(existing);

            // Ключ склейки - путь в кэше, а не URL: один и тот же файл может
            // прийти с разными адресами сервера (сменился туннель)
            string path = FilePathFor(url);
            return _inFlight.GetOrAdd(path, _ => DownloadAsync(url, path));
        }

        /// <summary>Содержимое файла: сначала кэш, потом сеть. null - не получилось.</summary>
        public static async Task<byte[]> GetBytesAsync(string url)
        {
            string path = await GetLocalPathAsync(url).ConfigureAwait(false);
            if (path == null) return null;

            try
            {
                return await File.ReadAllBytesAsync(path).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Save($"[MediaCache.GetBytes] {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Тихо кладёт файл в кэш в фоне (предзагрузка чата, докачка после
        /// просмотра видео). Ошибки только в лог: пользователь ничего не ждёт.
        /// </summary>
        public static void Prefetch(string url)
        {
            if (string.IsNullOrEmpty(url) || TryGetLocalPath(url, out _)) return;

            _ = Task.Run(async () =>
            {
                await _prefetchGate.WaitAsync().ConfigureAwait(false);
                try { await GetLocalPathAsync(url).ConfigureAwait(false); }
                catch (Exception ex) { Log.Save($"[MediaCache.Prefetch] {ex.GetType().Name}: {ex.Message}"); }
                finally { _prefetchGate.Release(); }
            });
        }

        private static async Task<string> DownloadAsync(string url, string path)
        {
            // Качаем во временный файл и переименовываем: если приложение закроют
            // посреди загрузки, в кэше не останется обрезанного файла, который
            // потом отдавался бы как целый. Имя временного - со случайным суффиксом:
            // тот же файл может качать второй запущенный экземпляр клиента.
            string temp = $"{path}.{Guid.NewGuid():N}.part";
            try
            {
                Directory.CreateDirectory(FilesDir);

                using (var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    using var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    using var target = new FileStream(temp, FileMode.Create, FileAccess.Write,
                                                      FileShare.None, 81920, useAsync: true);
                    await source.CopyToAsync(target).ConfigureAwait(false);
                }

                File.Move(temp, path, overwrite: true);
                ScheduleTrim();
                return path;
            }
            catch (Exception ex)
            {
                Log.Save($"[MediaCache.Download] {ex.GetType().Name}: {ex.Message} ({url})");
                try { if (File.Exists(temp)) File.Delete(temp); } catch { }
                return null;
            }
            finally
            {
                // Снимаем задачу из «в работе» - иначе неудачная попытка запомнилась бы
                // навсегда и файл больше никогда не докачался
                _inFlight.TryRemove(path, out _);
            }
        }

        // ── Превью (маленькие картинки для списка сообщений) ─────────────────

        /// <summary>
        /// Путь превью. Форматы с прозрачностью сохраняем в PNG, остальное - в JPEG:
        /// JPEG в разы меньше, но альфа-канал в нём превратился бы в чёрный фон.
        /// </summary>
        private static string PreviewPathFor(string url)
        {
            string key = Path.GetFileNameWithoutExtension(KeyFor(url));
            string ext = Path.GetExtension(ServerFileName(url)).ToLowerInvariant();
            bool alpha = ext == ".png" || ext == ".gif" || ext == ".webp";
            return Path.Combine(PreviewDir, key + (alpha ? ".png" : ".jpg"));
        }

        /// <summary>Готовое превью с диска - самый быстрый путь, без сети и без декодирования оригинала.</summary>
        public static bool TryLoadPreview(string url, out BitmapImage image)
        {
            image = null;
            if (string.IsNullOrEmpty(url)) return false;

            try
            {
                string path = PreviewPathFor(url);
                if (!File.Exists(path)) return false;

                var bitmap = new BitmapImage();
                // Читаем через поток с OnLoad: иначе BitmapImage держит файл открытым
                // и его нельзя ни удалить при чистке, ни перезаписать
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                }
                bitmap.Freeze();
                image = bitmap;
                return true;
            }
            catch (Exception ex)
            {
                Log.Save($"[MediaCache.LoadPreview] {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        /// <summary>Сохраняет превью, чтобы при следующем запуске его не строить заново.</summary>
        public static void SavePreview(string url, BitmapSource frame)
        {
            if (string.IsNullOrEmpty(url) || frame == null) return;

            try
            {
                Directory.CreateDirectory(PreviewDir);
                string path = PreviewPathFor(url);

                BitmapEncoder encoder = path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                    ? new PngBitmapEncoder()
                    : new JpegBitmapEncoder { QualityLevel = 85 };
                encoder.Frames.Add(BitmapFrame.Create(frame));

                // Тот же приём, что и с оригиналами: сначала временный файл
                string temp = $"{path}.{Guid.NewGuid():N}.part";
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                    encoder.Save(stream);
                File.Move(temp, path, overwrite: true);
            }
            catch (Exception ex)
            {
                Log.Save($"[MediaCache.SavePreview] {ex.GetType().Name}: {ex.Message}");
            }
        }

        // ── Обслуживание ─────────────────────────────────────────────────────

        private static int _trimBusy;

        /// <summary>Просит прибраться в кэше (в фоне, не чаще одной уборки разом).</summary>
        public static void ScheduleTrim()
        {
            if (Interlocked.Exchange(ref _trimBusy, 1) == 1) return;
            _ = Task.Run(() =>
            {
                try { TrimNow(); }
                catch (Exception ex) { Log.Save($"[MediaCache.Trim] {ex.GetType().Name}: {ex.Message}"); }
                finally { Volatile.Write(ref _trimBusy, 0); }
            });
        }

        private static void TrimNow()
        {
            if (!Directory.Exists(FilesDir)) return;

            var files = new DirectoryInfo(FilesDir).GetFiles();

            // Хвосты прерванных загрузок: живой .part не старше часа, остальные - мусор
            foreach (var part in files.Where(f => f.Extension == ".part"
                                                 && DateTime.UtcNow - f.LastWriteTimeUtc > TimeSpan.FromHours(1)))
            {
                try { part.Delete(); } catch { }
            }

            var kept = files.Where(f => f.Extension != ".part").ToList();
            long total = kept.Sum(f => f.Length);
            if (total <= MaxBytes) return;

            // Чистим с запасом (до 90% потолка), чтобы уборка не запускалась
            // после каждой следующей загрузки
            long target = (long)(MaxBytes * 0.9);
            foreach (var file in kept.OrderBy(f => f.LastWriteTimeUtc))
            {
                if (total <= target) break;
                try
                {
                    long size = file.Length;
                    file.Delete();
                    total -= size;
                    // Превью того же файла больше не нужно
                    TryDeletePreview(Path.GetFileNameWithoutExtension(file.Name));
                }
                catch { /* занят плеером - вытесним в следующий раз */ }
            }
        }

        private static void TryDeletePreview(string key)
        {
            foreach (string ext in new[] { ".jpg", ".png" })
            {
                try
                {
                    string path = Path.Combine(PreviewDir, key + ext);
                    if (File.Exists(path)) File.Delete(path);
                }
                catch { }
            }
        }

        /// <summary>Сколько занимает кэш - для строки в настройках.</summary>
        public static (int Files, long Bytes) Stats()
        {
            try
            {
                var all = new List<FileInfo>();
                foreach (string dir in new[] { FilesDir, PreviewDir })
                    if (Directory.Exists(dir)) all.AddRange(new DirectoryInfo(dir).GetFiles());

                return (all.Count, all.Sum(f => f.Length));
            }
            catch (Exception ex)
            {
                Log.Save($"[MediaCache.Stats] {ex.GetType().Name}: {ex.Message}");
                return (0, 0);
            }
        }

        /// <summary>Полная очистка (кнопка в настройках). Занятые файлы пропускаем.</summary>
        public static void Clear()
        {
            foreach (string dir in new[] { FilesDir, PreviewDir })
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var file in new DirectoryInfo(dir).GetFiles())
                {
                    try { file.Delete(); } catch { }
                }
            }
        }
    }
}
