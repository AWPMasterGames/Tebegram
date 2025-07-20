using System.Collections.ObjectModel;
using System.Net;
using TebegramServer.Classes;
using System.Linq;
using System.Text.Json;

namespace TebegramServer.Data
{
    public static class UsersData
    {
        static ObservableCollection<User> Users = new ObservableCollection<User>();

        static UsersData()
        {
            LoadUserList();
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
            Users.Add(user);
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
        }
    }
}
