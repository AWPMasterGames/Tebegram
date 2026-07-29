using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using Tebegrammmm.Data;

namespace Tebegrammmm.Classes
{
    /// <summary>
    /// Сущность «чат» — задел под ГРУППОВЫЕ чаты (перенос из main-dev, коммит
    /// 311bff0, с исправлениями). Пока НЕ используется текущим потоком данных:
    /// переписка по-прежнему живёт в Contact.Messages, потому что переход на
    /// чаты требует смены протокола (ChatId в сообщениях) и формата логина —
    /// это ломает совместимость со всеми выпущенными клиентами и делается
    /// отдельной миграцией (см. roadmap, Этап 23 п.5).
    ///
    /// Отличия от версии main-dev:
    /// — Avatar с INotifyPropertyChanged (как у Contact): аватар грузится
    ///   асинхронно ПОСЛЕ привязки UI, без уведомления список не обновится;
    /// — загрузка аватара не выполняется для пустого списка участников
    ///   (в оригинале UserId оставался 0 и клиент запрашивал /avatarsFileName/0);
    /// — к URL аватара добавляется ?t=… против кэша картинок WPF;
    /// — убран мусорный using Microsoft.VisualBasic.
    /// </summary>
    public class Chat : System.ComponentModel.INotifyPropertyChanged
    {
        static HttpClient httpClient = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (m, c, ch, e) => true
        });

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        private void Notify(string prop) =>
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(prop));

        private int _Id;
        private string _Name;
        private string _Avatar;
        private ObservableCollection<Message> _Messages;

        public int Id { get { return _Id; } }
        public string Name { get { return _Name; } }
        public ObservableCollection<Message> Messages { get { return _Messages; } }
        public ObservableCollection<Contact> Members { get; private set; }
        public bool IsGroup { get; set; }
        public bool IOwner { get; set; }

        public string Avatar
        {
            get { return _Avatar; }
            set { _Avatar = value; Notify(nameof(Avatar)); }
        }

        // ── Поверхность для списка чатов ─────────────────────────────────────
        // Список LBChats показывает и контакты, и группы одним шаблоном, поэтому
        // у Chat должны быть те же свойства, к которым привязан шаблон. Иначе
        // привязки к отсутствующим свойствам сыпали бы ошибками в окно вывода.
        public bool IsFavorites => false;        // «Избранное» — только личный чат с собой
        public bool IsGlobalResult => false;     // группа не бывает результатом @-поиска
        public string GlobalHint => string.Empty;
        public string Draft { get; set; } = string.Empty;

        /// <summary>История с сервера уже загружена? (чтобы не тянуть повторно)</summary>
        public bool HistoryLoaded { get; set; }

        /// <summary>Подпись под названием группы в списке — сколько участников.</summary>
        public string MembersHint => IsGroup && Members.Count > 0
            ? $"Участников: {Members.Count}"
            : string.Empty;

        public Chat(int id, string name, bool isGroup, string avatar, bool iOwner)
            : this(id, name, isGroup, avatar, iOwner, null, null)
        {
        }

        public Chat(int id, string name, bool isGroup, string avatar, bool iOwner,
                    ObservableCollection<Contact> members, ObservableCollection<Message> messages)
        {
            _Id = id;
            _Name = name;
            IsGroup = isGroup;
            IOwner = iOwner;
            _Avatar = avatar;
            Members = members ?? new ObservableCollection<Contact>();
            _Messages = messages ?? new ObservableCollection<Message>();
            LoadPeerAvatar();
        }

        /// <summary>
        /// Для НЕгрупповых чатов аватар чата = аватар собеседника.
        /// Групповые используют собственный Avatar, участники подгружаются позже.
        /// </summary>
        private async void LoadPeerAvatar()
        {
            if (IsGroup) return;

            // Ищем собеседника (не себя). Участники могут быть ещё не загружены —
            // тогда просто выходим, аватар подтянется при их заполнении.
            int peerId = 0;
            foreach (var member in Members)
            {
                if (UserData.User == null || member.UserId != UserData.User.Id)
                    peerId = member.UserId;
            }
            if (peerId == 0) return; // в оригинале здесь запрашивался /avatarsFileName/0

            try
            {
                await ServerData.Ready;
                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get,
                    $"{ServerData.ServerAdress}/avatarsFileName/{peerId}");
                using HttpResponseMessage response = await httpClient.SendAsync(request);
                string content = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(content))
                {
                    // ?t=… — тот же обход процессного кэша картинок WPF, что у Contact
                    Avatar = $"{ServerData.ServerAdress}/avatars/{content}?t={DateTime.UtcNow.Ticks}";
                }
            }
            catch (Exception ex)
            {
                Log.Save($"[Chat.LoadPeerAvatar] {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
