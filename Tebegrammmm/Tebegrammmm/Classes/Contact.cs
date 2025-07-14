using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Tebegrammmm
{
    public class Contact
    {
        private string _Name = string.Empty;
        private ObservableCollection<Message> _Messages;
        public IPAddress IPAddress { get; set; }
        public int Port { get; set; }
        public string Name { get { return _Name; } }
        public ObservableCollection<Message> Messages { get { return _Messages; } }
        public string Username { get; set; }
        public string Draft { get; set; } = string.Empty; // Черновик сообщения

        public Contact()
        {
            _Messages = new ObservableCollection<Message>();
        }
        public Contact(IPAddress iPAddress, int port, string name, string username = "")
        {
            IPAddress = iPAddress;  // Устанавливаем публичное свойство
            Port = port;            // Устанавливаем публичное свойство
            _Name = name;
            Username = username;
            _Messages = new ObservableCollection<Message>();
        }
        public Contact(IPAddress iPAddress, int port, string name, ObservableCollection<Message> messages)
        {
            IPAddress = iPAddress;  // Устанавливаем публичное свойство
            Port = port;            // Устанавливаем публичное свойство
            _Name = name;
            _Messages = messages;
        }

        public void ChangeName(string name)
        {
            _Name = name;
        }
        public void ChangeIpAdress(IPAddress ipAddress)
        {
            IPAddress = ipAddress;  // Устанавливаем публичное свойство
        }
        public void ChangePort(int port)
        {
            Port = port;            // Устанавливаем публичное свойство
        }
    }
}
