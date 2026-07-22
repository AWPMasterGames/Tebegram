using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tebegrammmm
{
    public enum MessageType
    {
        Text,
        Image,
        File
    }
    

    public enum MessageStatus
    {
        Sent,      // Доставлено
        Pending,   // Не доставлено (серый фон)
        Failed     // Ошибка отправки
    }
    public class Message : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        private string _Sender;
        private string _Reciver;
        private string _Text;
        private string _Time;

        private MessageType _MessageType;
        private string _FilePath;
        private string _ServerAdress;
        private MessageStatus _Status;
        public string Sender { get { return _Sender; } }
        public string Reciver { get { return _Reciver; } }
        public string Text { get { return _Text; } }
        public string Time { get { return _Time; } }
        public MessageType MessageType { get { return _MessageType; } }
        public string ServerAdress { get { return _ServerAdress; } }

        /// <summary>
        /// URL файла для показа/скачивания. Если сохранённый адрес потерян
        /// (сервер до фикса не хранил ServerAdress в базе — фото приходили «пустыми»),
        /// строим стандартный путь /upload/имя. Для не-файлов — null, чтобы
        /// картинка в пузыре не пыталась грузиться.
        /// </summary>
        public string FileUrl
        {
            get
            {
                if (_MessageType != MessageType.File) return null;
                if (!string.IsNullOrEmpty(_ServerAdress) && _ServerAdress.StartsWith("http"))
                    return _ServerAdress;
                return $"{Tebegrammmm.Data.ServerData.ServerAdress}/upload/{Uri.EscapeDataString(_Text ?? "")}";
            }
        }

        // ── Инлайн-превью фото ───────────────────────────────────────────────
        // Картинка грузится нашим HttpClient (как в просмотрщике) — WPF-загрузчик
        // по URI иногда молча не справлялся, и фото выглядели пустыми сообщениями.
        private static readonly System.Net.Http.HttpClient _imageHttp = new(new System.Net.Http.HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (m, c, ch, e) => true
        });
        private static readonly Dictionary<string, System.Windows.Media.Imaging.BitmapImage> _imageCache = new();
        private static readonly object _imageCacheLock = new();

        private System.Windows.Media.ImageSource _fileImage;
        private bool _fileImageRequested;

        // ── Классификация вложений по расширению ────────────────────────────
        // Три группы: картинка (превью в пузыре), «проигрываемое» медиа (открываем —
        // браузер/плеер это покажет) и ВСЁ ОСТАЛЬНОЕ — неизвестный файл, для которого
        // используется универсальная карточка со скачиванием. В список playable
        // попадают только форматы, которые браузер реально умеет открыть: mkv/avi/
        // архивы/документы туда не входят, иначе клик открывал бы пустую вкладку.
        // Списки согласованы с сервером (Program.cs, выбор inline/attachment)
        // и веб-клиентом (docs/app.js, FILE_KINDS).
        private static readonly HashSet<string> ImageExt = new()
            { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp" };
        private static readonly HashSet<string> VideoExt = new()
            { ".mp4", ".webm", ".ogv", ".mov" };
        private static readonly HashSet<string> AudioExt = new()
            { ".mp3", ".wav", ".ogg", ".m4a", ".aac", ".opus" };

        private string Ext => _MessageType != MessageType.File
            ? string.Empty
            : System.IO.Path.GetExtension(_Text ?? "").ToLowerInvariant();

        /// <summary>Файл-картинка? По расширению из Text (там лежит имя файла на сервере).</summary>
        public bool IsImageFile => ImageExt.Contains(Ext);

        /// <summary>Видео или аудио, которое открывается плеером/браузером.</summary>
        public bool IsPlayableFile => VideoExt.Contains(Ext) || AudioExt.Contains(Ext);

        /// <summary>
        /// Неизвестный файл (архив, документ, exe, редкий контейнер видео…).
        /// Показывается универсальной карточкой: открыть его нечем, поэтому
        /// единственное действие — скачать.
        /// </summary>
        public bool IsUnknownFile => _MessageType == MessageType.File && !IsImageFile && !IsPlayableFile;

        /// <summary>
        /// Файл НЕ-картинка (видео/аудио/документ). Рисуется чипом с именем файла —
        /// раньше такой файл шёл в путь картинки, декодирование молча падало и
        /// пузырь оставался пустым («видосы не отображаются»).
        /// </summary>
        public bool IsPlainFile => _MessageType == MessageType.File && !IsImageFile;

        /// <summary>
        /// Подпись под именем файла в чипе: что это и что произойдёт по клику.
        /// Для неизвестных типов показываем расширение («ZIP-файл»), чтобы было
        /// понятно, что скачивается.
        /// </summary>
        public string FileCaption
        {
            get
            {
                if (_MessageType != MessageType.File) return string.Empty;
                if (VideoExt.Contains(Ext)) return "Видео · открыть";
                if (AudioExt.Contains(Ext)) return "Аудио · открыть";
                string ext = Ext.TrimStart('.').ToUpperInvariant();
                return string.IsNullOrEmpty(ext) ? "Файл · скачать" : $"{ext}-файл · скачать";
            }
        }

        /// <summary>Имя файла для чипа.</summary>
        public string FileName => System.IO.Path.GetFileName(_Text ?? "");

        /// <summary>Готовая картинка для превью в пузыре (null, пока грузится или не фото).</summary>
        public System.Windows.Media.ImageSource FileImage
        {
            get
            {
                // Только для картинок: видео/аудио раньше скачивались целиком
                // ради заведомо провального декодирования в BitmapImage
                if (_fileImage == null && !_fileImageRequested && IsImageFile)
                {
                    _fileImageRequested = true;
                    _ = LoadFileImageAsync();
                }
                return _fileImage;
            }
        }

        // ── Превью видео: первый кадр ────────────────────────────────────────
        // Кадр рисуется MediaPlayer'ом в RenderTargetBitmap. Всё на UI-потоке:
        // MediaPlayer и RenderTargetBitmap требуют STA, из фонового потока падают.
        private static readonly Dictionary<string, System.Windows.Media.ImageSource> _videoThumbCache = new();
        private System.Windows.Media.ImageSource _videoThumb;
        private bool _videoThumbRequested;

        /// <summary>Первый кадр видео для превью в чате (null, пока не готов).</summary>
        public System.Windows.Media.ImageSource VideoThumbnail
        {
            get
            {
                if (_videoThumb == null && !_videoThumbRequested && IsVideoFile)
                {
                    _videoThumbRequested = true;
                    LoadVideoThumbnail();
                }
                return _videoThumb;
            }
        }

        /// <summary>Видеофайл? (для превью-кадра и открытия во встроенном плеере)</summary>
        public bool IsVideoFile => VideoExt.Contains(Ext);

        /// <summary>
        /// Показывать превью видео (кадр + значок play)? Обращение к VideoThumbnail
        /// заодно запускает выборку кадра. Пока кадра нет (грузится или нет кодека),
        /// сообщение выглядит как обычный файловый чип.
        /// </summary>
        public bool ShowVideoPreview => IsVideoFile && VideoThumbnail != null;

        /// <summary>Чип с именем файла — для всего, кроме картинок и видео с готовым превью.</summary>
        public bool ShowFileChip => IsPlainFile && !ShowVideoPreview;

        /// <summary>
        /// У фото и превью видео время рисуется полупрозрачной плашкой прямо на
        /// картинке (как в веб-клиенте), поэтому обычная строка времени под пузырём
        /// в этом случае не нужна — иначе время показывалось бы дважды.
        /// </summary>
        public bool ShowMediaTimeOverlay => IsImageFile || ShowVideoPreview;

        private void LoadVideoThumbnail()
        {
            string url = FileUrl;
            if (string.IsNullOrEmpty(url)) return;

            lock (_imageCacheLock)
            {
                if (_videoThumbCache.TryGetValue(url, out var cached))
                {
                    _videoThumb = cached;
                    foreach (string prop in new[] { nameof(VideoThumbnail), nameof(ShowVideoPreview), nameof(ShowFileChip), nameof(ShowMediaTimeOverlay) })
                        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(prop));
                    return;
                }
            }

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    // ScrubbingEnabled + Play/Pause — иначе кадр не декодируется и
                    // в RenderTargetBitmap попадает пустота
                    var player = new System.Windows.Media.MediaPlayer { Volume = 0, ScrubbingEnabled = true };
                    var timeout = new System.Windows.Threading.DispatcherTimer
                    { Interval = TimeSpan.FromSeconds(15) };

                    void Cleanup()
                    {
                        timeout.Stop();
                        try { player.Close(); } catch { }
                    }

                    player.MediaOpened += (_, _) =>
                    {
                        // Небольшой отступ от нуля: самый первый кадр часто чёрный
                        player.Position = TimeSpan.FromMilliseconds(300);
                        player.Play();
                        player.Pause();

                        // Даём декодеру отрисовать кадр, потом снимаем его
                        var grab = new System.Windows.Threading.DispatcherTimer
                        { Interval = TimeSpan.FromMilliseconds(400) };
                        grab.Tick += (_, _) =>
                        {
                            grab.Stop();
                            try
                            {
                                int w = player.NaturalVideoWidth, h = player.NaturalVideoHeight;
                                if (w <= 0 || h <= 0) { Cleanup(); return; }

                                // Уменьшаем до ширины превью — незачем держать полный кадр
                                double scale = Math.Min(1.0, 320.0 / w);
                                int tw = Math.Max(1, (int)(w * scale)), th = Math.Max(1, (int)(h * scale));

                                var visual = new System.Windows.Media.DrawingVisual();
                                using (var dc = visual.RenderOpen())
                                    dc.DrawVideo(player, new System.Windows.Rect(0, 0, tw, th));

                                var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(
                                    tw, th, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                                rtb.Render(visual);
                                rtb.Freeze();

                                lock (_imageCacheLock) _videoThumbCache[url] = rtb;
                                _videoThumb = rtb;
                                // Уведомляем и о видимости: пузырь переключается
                                // с файлового чипа на кадр с кнопкой play
                                foreach (string prop in new[] { nameof(VideoThumbnail), nameof(ShowVideoPreview), nameof(ShowFileChip), nameof(ShowMediaTimeOverlay) })
                                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(prop));
                            }
                            catch (Exception ex)
                            {
                                Classes.Log.Save($"[Message.VideoThumbnail] {ex.GetType().Name}: {ex.Message}");
                            }
                            finally { Cleanup(); }
                        };
                        grab.Start();
                    };

                    player.MediaFailed += (_, args) =>
                    {
                        // Нет кодека (mkv/avi и пр.) — превью не будет, покажем чип файла
                        Classes.Log.Save($"[Message.VideoThumbnail] не открылось: {args.ErrorException?.Message}");
                        Cleanup();
                    };

                    timeout.Tick += (_, _) => Cleanup(); // видео недоступно — не висим вечно
                    timeout.Start();

                    player.Open(new Uri(url, UriKind.Absolute));
                }
                catch (Exception ex)
                {
                    Classes.Log.Save($"[Message.VideoThumbnail] {ex.GetType().Name}: {ex.Message}");
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private async Task LoadFileImageAsync()
        {
            string url = FileUrl;
            if (string.IsNullOrEmpty(url)) return;

            lock (_imageCacheLock)
            {
                if (_imageCache.TryGetValue(url, out var cached))
                {
                    _fileImage = cached;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(FileImage)));
                    return;
                }
            }

            // До двух попыток: сервер мог быть занят/сеть моргнула
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    byte[] bytes = await _imageHttp.GetByteArrayAsync(url).ConfigureAwait(false);
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    using (var ms = new System.IO.MemoryStream(bytes))
                    {
                        bitmap.BeginInit();
                        bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = ms;
                        bitmap.EndInit();
                    }
                    bitmap.Freeze(); // можно использовать из любого потока

                    lock (_imageCacheLock) { _imageCache[url] = bitmap; }
                    _fileImage = bitmap;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(FileImage)));
                    return;
                }
                catch (Exception ex)
                {
                    Classes.Log.Save($"[Message.FileImage] Попытка {attempt + 1}: {ex.Message} ({url})");
                    if (attempt == 0) await Task.Delay(1500).ConfigureAwait(false);
                }
            }
            _fileImageRequested = false; // не вышло — позволим повторить при следующем обращении
        }

        public string Message_FilePath { get { return _FilePath; } }
        public MessageStatus Status { get { return _Status; } set { _Status = value; } }
        public bool IsOutgoing { get; set; } = false;

        public Message(string sender, string reciver, string text, string time, MessageType messageType = MessageType.Text, string serverAdress = null, string filePath = null)
        {
            _Sender = sender;
            _Reciver = reciver;
            _Text = text;
            _Time = time;
            _MessageType = messageType;
            _ServerAdress = serverAdress;
            _FilePath = filePath;
            _Status = MessageStatus.Sent; // По умолчанию
        }
        // ПЕРЕХОД НА ChatId: в протоколе v2 (main-dev) первым полем добавляется
        // {ChatId}▫ — тогда же нужно синхронно сдвинуть индексы разбора в
        // MessengerWindow.AddMessageToUser и добавить поле ChatId в этот класс.
        // Менять только ВМЕСТЕ с сервером (Tebegram-server/Classes/Message.ToString)
        // и вебом (docs/app.js: parseMessage/buildRaw) — иначе ломается доставка
        // у всех уже установленных клиентов.
        public override string ToString()
        {
            return $"{Sender}▫{Reciver}▫{MessageType}▫{Time}▫{ServerAdress}▫{Text}";
        }

        // ── Работа с датой/временем (формат хранения: dd.MM.yyyy HH:mm) ─────────
        private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");
        private static readonly string[] FullFormats = { "dd.MM.yyyy HH:mm", "dd.MM.yyyy H:mm" };

        private bool TryParseTime(out DateTime dt)
            => DateTime.TryParseExact(_Time, FullFormats, Ru, DateTimeStyles.None, out dt);

        /// <summary>Время для показа в пузыре — только ЧЧ:ММ (без даты).</summary>
        public string TimeShort
        {
            get
            {
                if (TryParseTime(out DateTime dt)) return dt.ToString("HH:mm");
                return _Time; // старый формат — показываем как есть
            }
        }

        /// <summary>Ключ группировки по дню (для разделителей-дат). Пустой = без группы.</summary>
        public string DateKey
        {
            get
            {
                if (TryParseTime(out DateTime dt)) return dt.ToString("yyyyMMdd");
                return "";
            }
        }

        /// <summary>Дружелюбная подпись даты, как в Telegram: Сегодня / Вчера / 4 июля / 4 июля 2025.</summary>
        public static string DateLabel(string dateKey)
        {
            if (string.IsNullOrEmpty(dateKey) ||
                !DateTime.TryParseExact(dateKey, "yyyyMMdd", Ru, DateTimeStyles.None, out DateTime dt))
                return "";

            DateTime today = DateTime.Today;
            if (dt.Date == today) return "Сегодня";
            if (dt.Date == today.AddDays(-1)) return "Вчера";
            if (dt.Year == today.Year) return dt.ToString("d MMMM", Ru);
            return dt.ToString("d MMMM yyyy", Ru);
        }
    }
}
