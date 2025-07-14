using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

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

        public IPAddress IpAddress;
        public int Port {get; set; }

        public ObservableCollection<ChatFolder> ChatsFolders { get; set; }

        public User(int id, string login, string password, string name, string ipadress, int port, ObservableCollection<ChatFolder> chatsFolders)
        {
            _Id = id;
            _Login = login;
            _Password = password;
            _Name = name;
            // Преобразуем localhost в IP-адрес для внутреннего использования
            if (ipadress.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                IpAddress = IPAddress.Parse("127.0.0.1");
            }
            else
            {
                IpAddress = IPAddress.Parse(ipadress);
            }
            Port = port;
            ChatsFolders = chatsFolders;
        }

        public bool Authorize(string login, string password)
        {
            if(login == _Login && password == _Password) return true;
            return false;
        }

        public string ToClientSend()
        {
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                Id = _Id,
                Login = _Login,
                Name = _Name,
                IpAddress = IpAddress.ToString() == "127.0.0.1" ? "localhost" : IpAddress.ToString(),
                Port = Port,
                ChatsFolders = ChatsFolders.Select(folder => new
                {
                    Id = folder.Id,
                    FolderName = folder.FolderName,
                    Icon = folder.Icon,
                    IsCanRedact = folder.IsCanRedact,
                    Contacts = folder.Contacts.Select(contact => new
                    {
                        Name = contact.Name,
                        IPAddress = contact.IPAddress.ToString(),
                        Port = contact.Port
                    }).ToArray()
                }).ToArray()
            });
        }

        public void EnsureServerPortConsistency()
        {
            if (Port != 5000)
            {
                Port = 5000;
            }
        }
    }
}
