using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Tebegrammmm
{
    public class User
    {
        private int _Id;
        private string _Login;
        private string _Password;
        private string _Name;

        public int Id { get { return _Id; } }
        public string Login { get { return _Login; } }
        public string Name { get { return _Name; } }

        public IPAddress IpAddress;
        public int Port {get; set; }

        public ObservableCollection<ChatFolder> ChatsFolders { get; set; }

        public User(int id, string login, string password, string name, string ipadress, int port, ObservableCollection<ChatFolder> chatsFolders)
        {
            _Id = id;
            _Login = login;
            _Password = password;
            _Name = name;
            IpAddress = IPAddress.Parse(ipadress);
            Port = port;
            ChatsFolders = chatsFolders;
        }

        public bool Authorize(string login, string password)
        {
            if(login == _Login & password == _Password) return true;
            return false;
        }

        public string ToClientSend()
        {
            // Формируем строку с данными пользователя для отправки клиенту
            StringBuilder sb = new StringBuilder();
            sb.Append($"{Id}▫");
            sb.Append($"{Login}▫");
            sb.Append($"{Name}▫");
            
            // Добавляем количество чат-папок
            sb.Append($"{ChatsFolders.Count}▫");
            
            // Для каждой папки добавляем информацию
            foreach (var folder in ChatsFolders)
            {
                sb.Append($"{folder.Id}▫");
                sb.Append($"{folder.FolderName}▫");
                sb.Append($"{folder.Icon}▫");
                sb.Append($"{folder.IsCanRedact}▫");
                sb.Append($"{folder.Contacts.Count}▫");
                
                // Для каждого контакта в папке
                foreach (var contact in folder.Contacts)
                {
                    sb.Append($"{contact.Name}▫");
                    // Вместо IP и порта используем имя пользователя
                    // sb.Append($"{contact.IPAddress}▫{contact.Port}▫");
                }
            }
            
            return sb.ToString();
        }
    }
}
