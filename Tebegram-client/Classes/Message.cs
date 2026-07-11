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

        /// <summary>Готовая картинка для превью в пузыре (null, пока грузится или не фото).</summary>
        public System.Windows.Media.ImageSource FileImage
        {
            get
            {
                if (_fileImage == null && !_fileImageRequested && _MessageType == MessageType.File)
                {
                    _fileImageRequested = true;
                    _ = LoadFileImageAsync();
                }
                return _fileImage;
            }
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
