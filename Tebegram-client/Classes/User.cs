using System.Collections.ObjectModel;
using Tebegrammmm.Classes;

namespace Tebegrammmm
{
    public class User
    {
        private int _Id;
        private string _Login;
        private string _Password;
        private string _Name;

        public int Id { get { return _Id; } }
        public string Avatar { get; set; }
        public string Login { get { return _Login; } }
        public string Password { get { return _Password; } }
        public string Name { get { return _Name; } }

        public string Username { get; set; }

        public ObservableCollection<ChatFolder> ChatsFolders { get; set; }
        public ObservableCollection<Contact> Contacts { get { return ChatsFolders[0].Contacts; } set { ChatsFolders[0].Contacts = value; } }
        public ObservableCollection<Chat> Chats { get { return ChatsFolders[0].Chats; } set { ChatsFolders[0].Chats = value; } }

        public bool InCall { get; set; }
        public int SelectedDeviceNum{ get; set; }
        public string SelectedDeviceName{ get; set; }

        public User(int id, string login, string password, string name, string username, ObservableCollection<ChatFolder> chatsFolders, string avatar)
        {
            _Id = id;
            Avatar = avatar;
            _Login = login;
            _Password = password;
            _Name = name;
            Username = username;
            ChatsFolders = chatsFolders;
        }

        public void AddContact(Contact contact)
        {
            ChatsFolders[0].Contacts.Add(contact);
        }

        public void AddChat(Chat chat)
        {
            ChatsFolders[0].Chats.Add(chat);
        }

        public bool Authorize(string login, string password)
        {
            if (login == _Login & password == _Password) return true;
            return false;
        }

        public Contact FindContactByUsername(string username)
        {
            foreach (Contact contact in ChatsFolders[0].Contacts)
            {
                if (contact.Username == username) return contact;
            }
            return null;
        }
        public Chat FindChatbyId(int id)
        {
            foreach (Chat Chat in ChatsFolders[0].Chats)
            {
                if (Chat.Id == id) return Chat;
            }
            return null;
        }
    }
}
