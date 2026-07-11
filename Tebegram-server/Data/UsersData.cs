using System.Collections.ObjectModel;
using System.Net;
using TebegramServer.Classes;
using System.Linq;
using System.Text.Json;
using System.Timers;

namespace TebegramServer.Data
{
    public static class UsersData
    {
        static ObservableCollection<User> Users = new ObservableCollection<User>();
        public static int UsersCount { get {  return Users.Count; } }
        private static System.Timers.Timer saveTimer;

        static UsersData()
        {
            LoadUserList();
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Данные пользователей загружены перед запуском сервера");

            // Настраиваем таймер для автоматического сохранения каждые 10 секунд
            // Только если есть пользователи для сохранения
            saveTimer = new System.Timers.Timer(10000); // 10 секунд
            saveTimer.Elapsed += (sender, e) => SaveUserToFile();
            saveTimer.AutoReset = true;
            saveTimer.Start();
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Автосохранение запущено (каждые 10 секунд)");
        }

        public static bool IsExistUser(string login)
        {
            return Users.Any(user => user.Login == login);
        }

        public static User? Authorize(string login, string password)
        {
            return Users.FirstOrDefault(user => user.Authorize(login,password));
        }
        public static User? FindUserById(int id)
        {
            return Users.FirstOrDefault(user => user.Id == id);
        }
        public static User? FindUserByLogin(string login)
        {
            return Users.FirstOrDefault(user => user.Login == login);
        }
        public static User? FindUserByUsername(string username)
        {
            return Users.FirstOrDefault(user => user.Username == username);
        }

        /// <summary>
        /// Поиск по подстроке логина или имени (для глобального поиска через @).
        /// Порядок: точное совпадение логина → логин начинается с запроса → остальные вхождения.
        /// </summary>
        public static List<User> FindUsers(string query, int limit = 20)
        {
            string q = query.ToLowerInvariant();
            return Users.ToList()
                .Where(u => (u.Username?.ToLowerInvariant().Contains(q) ?? false)
                         || (u.Name?.ToLowerInvariant().Contains(q) ?? false))
                .OrderBy(u => u.Username.ToLowerInvariant() == q ? 0
                            : u.Username.ToLowerInvariant().StartsWith(q) ? 1 : 2)
                .ThenBy(u => u.Username, StringComparer.OrdinalIgnoreCase)
                .Take(limit)
                .ToList();
        }
        
        public static void AddUser(User user)
        {
            if (user != null && !Users.Any(u => u.Login == user.Login))
            {
                Users.Add(user);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Добавлен новый пользователь: {user.Login} с ID {user.Id}");
            }
        }
        
        public static int GetNextUserId()
        {
            return Users.Any() ? Users.Max(u => u.Id) + 1 : 1;
        }

        /// <summary>
        /// Сбрасывает токен звонка у ВСЕХ участников: у звонившего он хранится как «token»,
        /// у вызываемого — как «caller▫token». Иначе после завершения звонка токен зависал
        /// у второй стороны (фантомный входящий звонок).
        /// </summary>
        public static void ClearCallTokens(string token)
        {
            if (string.IsNullOrEmpty(token)) return;
            foreach (var user in Users.ToList())
            {
                if (user.CallToken == token || (user.CallToken?.EndsWith($"▫{token}") ?? false))
                    user.CallToken = "";
            }
        }
        
        // Метод для принудительной инициализации данных перед запуском сервера
        public static void Initialize()
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Инициализация UsersData завершена. Пользователей в памяти: {Users.Count}");
        }
        
        public static void SaveUserToFile()
        {
            try
            {
                // Если нет пользователей, не сохраняем (чтобы не затереть файл)
                if (!Users.Any())
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Автосохранение пропущено: нет пользователей для сохранения");
                    return;
                }

                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Users.json");

                // Создаем директорию, если она не существует
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

                List<UserData> usersData = new List<UserData>();

                // Проходим по снимкам коллекций (.ToList()) — иначе автосохранение падает,
                // если в этот момент другой поток добавляет сообщение или контакт
                foreach (var currentUser in Users.ToList())
                {
                    // Конвертируем User в UserData
                    var userData = new UserData
                    {
                        Id = currentUser.Id,
                        Login = currentUser.Login,
                        Password = currentUser.Password,
                        Name = currentUser.Name,
                        Username = currentUser.Username,
                        Avatart = currentUser.Avatar,
                        ChatsFolders = currentUser.ChatsFolders.ToList().Select(folder => new ChatFolderData
                        {
                            Name = folder.FolderName,
                            Icon = folder.Icon,
                            CanDelete = folder.IsCanRedact,
                            Contacts = folder.Contacts.ToList().Select(contact => new ContactData
                            {
                                Id = contact.UserId,
                                Username = contact.Username,
                                Name = contact.Name,
                                Messages = contact.Messages.ToList().Select(message => new MessageData
                                {
                                    Sender = message.Sender,
                                    Recipient = message.Reciver,
                                    Text = message.Text,
                                    Time = message.Time, // Время храним как есть — конвертация ломала формат и сдвигала часы
                                    MessageType = message.MessageType.ToString(),
                                    ServerAdress = message.ServerAdress ?? "", // без него фото после рестарта сервера теряли URL
                                    MessageString = message.ToString() // Используем ToString() из Message
                                }).ToList()
                            }).ToList()
                        }).ToList()
                    };

                    // Добавляем пользователя в список для сохранения
                    usersData.Add(userData);
                }

                // Сохраняем в файл с форматированием
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                string jsonContent = JsonSerializer.Serialize(usersData, options);
                File.WriteAllText(filePath, jsonContent);

                //Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Автосохранение: {Users.Count} пользователей сохранено в файл");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Ошибка при автосохранении пользователей: {ex.Message}");
            }
        }

        public static void StopAutoSave()
        {
            saveTimer?.Stop();
            saveTimer?.Dispose();
        }

        public static void SaveAllUsers()
        {
            try
            {
                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Users.json");
                
                // Создаем директорию, если она не существует
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                
                var usersData = Users.Select(user => new UserData
                {
                    Id = user.Id,
                    Login = user.Login,
                    Password = user.Password,
                    Name = user.Name,
                    Username = user.Username,
                    Avatart = user.Avatar,
                    ChatsFolders = user.ChatsFolders.Select(folder => new ChatFolderData
                    {
                        Name = folder.FolderName,
                        Icon = folder.Icon,
                        CanDelete = folder.IsCanRedact,
                        Contacts = folder.Contacts.Select(contact => new ContactData
                        {
                            Id = contact.UserId,
                            Username = contact.Username,
                            Name = contact.Name,
                            Messages = contact.Messages.Select(message => new MessageData
                            {
                                Sender = message.Sender,
                                Recipient = message.Reciver,
                                Text = message.Text,
                                Time = message.Time,
                                MessageType = message.MessageType.ToString(),
                                ServerAdress = message.ServerAdress ?? "",
                                MessageString = message.ToString()
                            }).ToList()
                        }).ToList()
                    }).ToList()
                }).ToList();
                
                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                string jsonContent = JsonSerializer.Serialize(usersData, options);
                File.WriteAllText(filePath, jsonContent);
                
                Console.WriteLine($"Все пользователи ({Users.Count}) сохранены в файл");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при сохранении всех пользователей: {ex.Message}");
            }
        }

        private static void LoadUserList()
        {
            try
            {
                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Users.json");
                
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"Файл пользователей не найден: {filePath}");
                    return;
                }

                string jsonContent = File.ReadAllText(filePath);
                var usersData = JsonSerializer.Deserialize<List<UserData>>(jsonContent);

                if (usersData != null)
                {
                    foreach (var userData in usersData)
                    {
                        var chatsFolders = new ObservableCollection<ChatFolder>();
                        
                        foreach (var folderData in userData.ChatsFolders)
                        {
                            var contacts = new ObservableCollection<Contact>();
                            
                            foreach (var contactData in folderData.Contacts)
                            {
                                var messages = new ObservableCollection<Message>();
                                
                                foreach (var messageData in contactData.Messages)
                                {
                                    var messageType = Enum.TryParse<MessageType>(messageData.MessageType, out var type) ? type : MessageType.Text;
                                    // ServerAdress восстанавливаем — раньше терялся, и фото после
                                    // рестарта сервера приходили клиентам с пустым URL (пустые пузыри)
                                    string? serverAdress = string.IsNullOrEmpty(messageData.ServerAdress) ? null : messageData.ServerAdress;
                                    messages.Add(new Message(messageData.Sender, messageData.Recipient, messageData.Text, messageData.Time, messageType, serverAdress));
                                }
                                
                                contacts.Add(new Contact(contactData.Id,contactData.Username, contactData.Name, messages));
                            }
                            
                            chatsFolders.Add(new ChatFolder(folderData.Name, contacts, folderData.Icon, folderData.CanDelete));
                        }
                        
                        var loadedUser = new User(userData.Id, userData.Login, userData.Password, userData.Name, userData.Username, chatsFolders, userData.Avatart);
                        loadedUser.EnsureFavorites(); // у всех существующих юзеров появляется «Избранное»
                        Users.Add(loadedUser);
                    }
                }
                
                Console.WriteLine($"Загружено пользователей: {Users.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при загрузке пользователей: {ex.Message}");
            }
        }

        // Вспомогательные классы для десериализации JSON
        private class UserData
        {
            public int Id { get; set; }
            public string Login { get; set; } = "";
            public string Password { get; set; } = "";
            public string Name { get; set; } = "";
            public string Username { get; set; } = "";
            public string Avatart { get; set; } = "";
            public List<ChatFolderData> ChatsFolders { get; set; } = new();
        }

        private class ChatFolderData
        {
            public string Name { get; set; } = "";
            public List<ContactData> Contacts { get; set; } = new();
            public string Icon { get; set; } = "";
            public bool CanDelete { get; set; }
        }

        private class ContactData
        {
            public int Id { get; set; }
            public string Username { get; set; } = "";
            public string Name { get; set; } = "";
            public List<MessageData> Messages { get; set; } = new();
        }

        private class MessageData
        {
            public string Sender { get; set; } = "";
            public string Recipient { get; set; } = "";
            public string Text { get; set; } = "";
            public string Time { get; set; } = "";
            public string MessageType { get; set; } = "Text";
            public string ServerAdress { get; set; } = ""; // URL файла (для File-сообщений)
            public string MessageString { get; set; } = "";
        }
    }
}
