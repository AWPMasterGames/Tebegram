using System.Collections.ObjectModel;
using System.Net.WebSockets;
using System.Text;
using TebegramServer.Classes;
using TebegramServer.Controllers;
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

            // Для каждого Чата в папке
            foreach (Chat chat in folder.Chats)
            {
                string owner = chat.Owner != null ? $"{chat.Owner?.Id}" : "None";
                string membersId = string.Empty;
                foreach (User u in chat.Members)
                {
                    membersId += $"{u.Id},";
                }
                if (!chat.IsGroup)
                {
                    User otherUser;

                    if (chat.Members[0].Id == this.Id) otherUser = chat.Members[1];
                    else otherUser = chat.Members[0];
                        string avatar = string.Empty;
                    if (string.IsNullOrEmpty(chat.Avatar))
                    {
                        avatar = otherUser.Avatar;
                    }

                    string name = string.Empty;
                    if (string.IsNullOrEmpty(chat.Name))
                    {
                        name = otherUser.Name;
                    }

                    sb.Append($"{chat.Id}&{name}&{chat.IsGroup}&{avatar}&{owner}&{membersId}▫");
                    continue;
                }
                sb.Append($"{chat.Id}&{chat.Name}&{chat.IsGroup}&{chat.Avatar}&{owner}&{membersId}▫");
                // Вместо IP и порта используем имя пользователя
                // sb.Append($"{contact.IPAddress}▫{contact.Port}▫");
            }

            // Для каждого контакта в папке
            //foreach (Chat chat in folder.Chats)
            //{
            //    sb.Append($"{chat.Id}&{chat.}&{contact.Name}▫");
            //    // Вместо IP и порта используем имя пользователя
            //    // sb.Append($"{contact.IPAddress}▫{contact.Port}▫");
            //}
            //}

            return sb.ToString();
        }

        public void AddChat(int chatId)
        {
            Chats.Add(chatId);
            ChatsFolders[0].Chats.Add(ChatsController.Chats[chatId]);
        }

        public void AddContact(Contact contact)
        {
            ChatsFolders[0].Contacts.Add(contact);
        }
        public void RemoveContact(Contact contact)
        {
            ChatsFolders[0].Contacts.Remove(contact);
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
            if (message.Sender == Username)
            {
                Contact contact = FindContactByUsername(message.Reciver);
                if (FindContactByUsername(message.Reciver) == null)
                {
                    User uConact = UsersData.FindUserByUsername(message.Reciver);
                    contact = new Contact(uConact.Id, uConact.Username, uConact.Name);
                    Contacts.Add(contact);
                }
                FindContactByUsername(message.Reciver).Messages.Add(message);
            }
            else if (FindContactByUsername(message.Sender) == null)
            {
                User uConact = UsersData.FindUserByUsername(message.Sender);
                Contact contact = new Contact(uConact.Id, uConact.Username, uConact.Name);
                contact.Messages.Add(message);
                AddContact(contact);
            }
            else if (message.Sender != Username)
            {
                FindContactByUsername(message.Sender).Messages.Add(message);
            }
        }
    }
}
