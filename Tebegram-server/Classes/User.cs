using System.Collections.ObjectModel;
using System.Net.WebSockets;
using System.Text;
using TebegramServer.Classes;
using TebegramServer.Data;

namespace TebegramServer
{
    public class User
    {
        private int _Id;
        private string _Login;
        private string _Password;
        private string _Name;

        public int Id { get { return _Id; } }
        public string Login { get { return _Login; } }
        public string Password { get { return _Password; } }
        public string Name { get { return _Name; } }

        public string Avatar { get; set; }
        public string Username { get; set; }
        public string CallToken { get; set; }

        public ObservableCollection<ChatFolder> ChatsFolders { get; set; }
        public ObservableCollection<Contact> Contacts { get { return ChatsFolders[0].Contacts; } }
        public ObservableCollection<int> Chats = new ObservableCollection<int>();

        public ObservableCollection<Message> NewMessages = new ObservableCollection<Message>();
        public ObservableCollection<WebSocket> ChatsSessions = new ObservableCollection<WebSocket>();

        // ── Платформа сессии ─────────────────────────────────────────────────
        // Звонок должен приходить ТОЛЬКО туда, откуда звонят: с win-клиента — на
        // win-клиент собеседника, из веба — в веб. Раньше токен звонка был один на
        // пользователя, его опрашивали обе платформы, и звонок звонил сразу везде.
        //
        // Платформу сообщает сам клиент при подключении чат-сокета
        // (/Chat/ws?userId=..&platform=win|web). Старые клиенты её не шлют — такая
        // сессия помечается "legacy" и считается доступной для любого звонка,
        // иначе выпущенные версии перестали бы принимать вызовы.
        private readonly Dictionary<WebSocket, string> _sessionPlatform = new();

        public void AddSession(WebSocket socket, string platform)
        {
            lock (_sessionPlatform)
            {
                _sessionPlatform[socket] = string.IsNullOrWhiteSpace(platform) ? "legacy" : platform;
            }
            ChatsSessions.Add(socket);
        }

        public void RemoveSession(WebSocket socket)
        {
            lock (_sessionPlatform) { _sessionPlatform.Remove(socket); }
            ChatsSessions.Remove(socket);
        }

        /// <summary>
        /// Есть ли живое подключение с этой платформы? Сессия без платформы
        /// (старый клиент) считается подходящей для любой — иначе звонок таким
        /// пользователям вообще перестал бы доходить.
        /// </summary>
        public bool IsOnlineOn(string platform)
        {
            lock (_sessionPlatform)
            {
                foreach (var pair in _sessionPlatform)
                {
                    if (pair.Key.State != WebSocketState.Open) continue;
                    if (pair.Value == platform || pair.Value == "legacy") return true;
                }
                return false;
            }
        }

        public User(int id, string login, string password, string name, string username, ObservableCollection<ChatFolder> chatsFolders, string avatar)
        {
            _Id = id;
            _Login = login;
            _Password = password;
            _Name = name;
            Username = username;
            ChatsFolders = chatsFolders;
            Avatar = avatar;
        }

        public bool Authorize(string login, string password)
        {
            if (login == _Login & password == _Password) return true;
            return false;
        }

        // ПЕРЕХОД НА ChatId: ответ логина намеренно в СТАРОМ формате (контакты) —
        // его разбирают все выпущенные клиенты. В v2 сюда добавляется блок чатов
        // (как в main-dev: {chat.Id}&{name}&{IsGroup}&{avatar}&{ownerId}&{memberIds}▫),
        // но с фиксами оригинала: для 1:1-чата имя/аватар собеседника подставлять
        // ТОЛЬКО при пустых полях чата (в main-dev при заполненных слались пустые
        // строки), а для чата с собой («Избранное») участник ОДИН — обращение к
        // Members[1] там падает. Готовый пример выдачи — эндпоинт /Chat/Create в Program.cs.
        public string ToClientSend()
        {
            // Формируем строку с данными пользователя для отправки клиенту
            StringBuilder sb = new StringBuilder();
            sb.Append($"{Id}▫");
            sb.Append($"{Login}▫");
            sb.Append($"{Name}▫");
            sb.Append($"{Username}▫");
            sb.Append($"{Avatar}▫");

            // Добавляем количество чат-папок
            //sb.Append($"{ChatsFolders.Count}▫");

            // Для каждой папки добавляем информацию
            /*foreach (var folder in ChatsFolders)
            {*/

            ChatFolder folder = ChatsFolders[0];
            //sb.Append($"{folder.Id}▫");
            sb.Append($"{folder.FolderName}▫");
            sb.Append($"{folder.Icon}▫");
            sb.Append($"{folder.IsCanRedact}▫");
            sb.Append($"{folder.Contacts.Count}▫");

            // Для каждого контакта в папке
            foreach (var contact in folder.Contacts)
            {
                sb.Append($"{contact.UserId}&{contact.Username}&{contact.Name}▫");
                // Вместо IP и порта используем имя пользователя
                // sb.Append($"{contact.IPAddress}▫{contact.Port}▫");
            }
            //}

            return sb.ToString();
        }

        public void AddChat(int chatId)
        {
            Chats.Add(chatId);

            // Перенос из main-dev (коммит 311bff0): чат кладётся и в папку «Все чаты»,
            // чтобы у пользователя была живая коллекция объектов, а не только id.
            // С защитой TryGetValue — в оригинале голый индексатор кидал
            // KeyNotFoundException, если комнаты с таким id нет в ChatsController
            // (например, после рестарта сервера — чаты пока не сохраняются в базу).
            if (Controllers.ChatsController.Chats.TryGetValue(chatId, out var chat))
                ChatsFolders[0].AddChat(chat);
        }

        public void RemoveChat(int chatId)
        {
            Chats.Remove(chatId);
        }

        public const string FavoritesName = "Избранное";

        /// <summary>
        /// Гарантирует наличие «Избранного» — чата с самим собой (как в Telegram).
        /// Это контакт с собственным username, всегда первым в списке.
        /// </summary>
        public void EnsureFavorites()
        {
            Contact? existing = FindContactByUsername(Username);
            if (existing == null)
            {
                ChatsFolders[0].Contacts.Insert(0, new Contact(Id, Username, FavoritesName));
            }
            else if (ChatsFolders[0].Contacts.IndexOf(existing) != 0)
            {
                // Держим Избранное первым в списке
                ChatsFolders[0].Contacts.Remove(existing);
                ChatsFolders[0].Contacts.Insert(0, existing);
            }
        }

        public void AddContact(Contact contact)
        {
            ChatsFolders[0].Contacts.Add(contact);
        }
        public void RemoveContact(Contact contact)
        {
            ChatsFolders[0].Contacts.Remove(contact);
        }
        public Contact? FindContactByUsername(string username)
        {
            foreach (Contact contact in ChatsFolders[0].Contacts)
            {
                if (contact.Username == username) return contact;
            }
            return null;
        }
        public string GetNewMessages()
        {
            string messages = string.Empty;
            if (NewMessages.Count == 0) return "NotFound";
            foreach (Message message in NewMessages)
            {
                messages += message.ToString();
            }
            NewMessages.Clear();
            return messages;
        }
        public void AddMessage(Message message)
        {
            // Чат с собой (Избранное): sender == reciver == я — кладём один раз
            if (message.Sender == Username && message.Reciver == Username)
            {
                EnsureFavorites();
                FindContactByUsername(Username)!.Messages.Add(message);
                return;
            }

            if (message.Sender == Username)
            {
                Contact? contact = FindContactByUsername(message.Reciver);
                if (contact == null)
                {
                    User? uConact = UsersData.FindUserByUsername(message.Reciver);
                    if (uConact == null) return; // получатель не зарегистрирован — раньше тут падал NullReferenceException
                    contact = new Contact(uConact.Id, uConact.Username, uConact.Name);
                    Contacts.Add(contact);
                }
                contact.Messages.Add(message);
            }
            else
            {
                Contact? contact = FindContactByUsername(message.Sender);
                if (contact == null)
                {
                    User? uConact = UsersData.FindUserByUsername(message.Sender);
                    if (uConact == null) return;
                    contact = new Contact(uConact.Id, uConact.Username, uConact.Name);
                    AddContact(contact);
                }
                contact.Messages.Add(message);
            }
        }

        /// <summary>Удаляет сообщение из указанного контакта по времени и тексту.</summary>
        public bool DeleteMessage(string contactUsername, string time, string text)
        {
            Contact? contact = FindContactByUsername(contactUsername);
            if (contact == null) return false;
            for (int i = contact.Messages.Count - 1; i >= 0; i--)
            {
                if (contact.Messages[i].Time == time && contact.Messages[i].Text == text)
                {
                    contact.Messages.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }
    }
}
