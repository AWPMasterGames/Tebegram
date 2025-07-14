using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TebegramServer
{
    public class Contact
    {
        private IPAddress _IPAddress;
        private int _Port;
        private string _Name = string.Empty;
        private string _Username = string.Empty;
        private ObservableCollection<Message> _Messages;
        public IPAddress IPAddress { get { return _IPAddress; } }
        public int Port { get { return _Port; } }
        public string Name { get { return _Name; } }
        public string Username { get { return _Username; } }
        public ObservableCollection<Message> Messages { get { return _Messages; } }

        public Contact()
        {
            _IPAddress = IPAddress.Loopback;
            _Port = 0;
            _Messages = new ObservableCollection<Message>();
        }
        
        public Contact(IPAddress iPAddress, int port, string name, string username = "")
        {
            _IPAddress = iPAddress;
            _Port = port;
            _Name = name;
            _Username = username;
            _Messages = new ObservableCollection<Message>();
        }
        
        public Contact(IPAddress iPAddress, int port, string name, ObservableCollection<Message> messages, string username = "")
        {
            _IPAddress = iPAddress;
            _Port = port;
            _Name = name;
            _Username = username;
            _Messages = messages;
        }

        public void ChangeName(string name)
        {
            _Name = name;
        }
        public void ChangeUsername(string username)
        {
            _Username = username;
        }
        public void ChangeIpAdress(IPAddress ipAddress)
        {
            _IPAddress = ipAddress;
        }
        public void ChangePort(int port)
        {
            _Port = port;
        }
    }
}
