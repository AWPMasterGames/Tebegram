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
        
        // Метод для принудительной инициализации данных перед запуском сервера
        public static void Initialize()
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Инициализация UsersData завершена. Пользователей в памяти: {Users.Count}");
        }
        
        // Метод для конвертации времени в часовой пояс пользователя
        private static string ConvertToUserTimeZone(string timeString)
        {
            try
            {
                // Пытаемся распарсить время и конвертировать в локальное время
                if (DateTime.TryParse(timeString, out DateTime dateTime))
                {
                    // Конвертируем в локальное время пользователя
                    return dateTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
                }
                return timeString; // Если не удалось распарсить, возвращаем как есть
            }
            catch
            {
                return timeString; // При ошибке возвращаем оригинальное время
            }
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

                // Проходим по всем пользователям из коллекции Users
                foreach (var currentUser in Users)
                {
                    // Конвертируем User в UserData
                    var userData = new UserData
                    {
                        Id = currentUser.Id,
                        Login = currentUser.Login,
                        Password = currentUser.Password,
                        Name = currentUser.Name,
                        Username = currentUser.Username,
                        ChatsFolders = currentUser.ChatsFolders.Select(folder => new ChatFolderData
                        {
                            Name = folder.FolderName,
                            Icon = folder.Icon,
                            CanDelete = folder.IsCanRedact,
                            Contacts = folder.Contacts.Select(contact => new ContactData
                            {
                                Username = contact.Username,
                                Name = contact.Name,
                                Messages = contact.Messages.Select(message => new MessageData
                                {
                                    Sender = message.Sender,
                                    Recipient = message.Reciver,
                                    Text = message.Text,
                                    Time = ConvertToUserTimeZone(message.Time), // Конвертируем время в часовой пояс пользователя
                                    MessageType = message.MessageType.ToString(),
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

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Автосохранение: {Users.Count} пользователей сохранено в файл");
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
                    ChatsFolders = user.ChatsFolders.Select(folder => new ChatFolderData
                    {
                        Name = folder.FolderName,
                        Icon = folder.Icon,
                        CanDelete = folder.IsCanRedact,
                        Contacts = folder.Contacts.Select(contact => new ContactData
                        {
                            Username = contact.Username,
                            Name = contact.Name,
                            Messages = contact.Messages.Select(message => new MessageData
                            {
                                Sender = message.Sender,
                                Recipient = message.Reciver,
                                Text = message.Text,
                                Time = ConvertToUserTimeZone(message.Time),
                                MessageType = message.MessageType.ToString(),
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
                                    messages.Add(new Message(messageData.Sender, messageData.Recipient, messageData.Text, messageData.Time, messageType));
                                }
                                
                                contacts.Add(new Contact(contactData.Username, contactData.Name, messages));
                            }
                            
                            chatsFolders.Add(new ChatFolder(folderData.Name, contacts, folderData.Icon, folderData.CanDelete));
                        }
                        
                        Users.Add(new User(userData.Id, userData.Login, userData.Password, userData.Name, userData.Username, chatsFolders));
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
            public string MessageString { get; set; } = "";
        }
    }
}
