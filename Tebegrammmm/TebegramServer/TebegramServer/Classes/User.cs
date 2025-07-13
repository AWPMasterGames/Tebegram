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
            return "Succes";
        }
    }
}
