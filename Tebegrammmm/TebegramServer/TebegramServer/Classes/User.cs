namespace TebegramServer.Classes
{
    public class User
    {
        private string _Login;
        private string _Password;

        public string Login { get { return _Login; } }
<<<<<<< Updated upstream
        public string Password { get { return _Password; } }
        public User(string login, string password)
=======
        public string Name { get { return _Name; } }

        public string Username { get; set; }

        public ObservableCollection<ChatFolder> ChatsFolders { get; set; }
        public ObservableCollection<Message> NewMessages = new ObservableCollection<Message>();

        public User(int id, string login, string password, string name, string username, ObservableCollection<ChatFolder> chatsFolders)
>>>>>>> Stashed changes
        {
            _Login = login;
            _Password = password;
        }

        public bool Authorize(string login, string password)
        {
            if (login == _Login & password == _Password) return true;
            return false;
        }
<<<<<<< Updated upstream
=======

        public string ToClientSend()
        {
            // Формируем строку с данными пользователя для отправки клиенту
            StringBuilder sb = new StringBuilder();
            sb.Append($"{Id}▫");
            sb.Append($"{Login}▫");
            sb.Append($"{Name}▫");
            sb.Append($"{Username}▫");

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
                sb.Append($"{contact.Username}&{contact.Name}▫");
                // Вместо IP и порта используем имя пользователя
                // sb.Append($"{contact.IPAddress}▫{contact.Port}▫");
            }
            //}

            return sb.ToString();
        }
        public Contact FindContactByUsername(string username)
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
            if(NewMessages.Count == 0) return "NotFound";
            foreach(Message message in NewMessages)
            {
                messages += message.ToString();
            }
            NewMessages.Clear();
            return messages;
        }
<<<<<<< Updated upstream
>>>>>>> Stashed changes
=======
>>>>>>> Stashed changes
    }
}
