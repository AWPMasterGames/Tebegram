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
        private string _Name = string.Empty;
        private ObservableCollection<Message> _Messages;
        public string Name { get { return _Name; } }
        public ObservableCollection<Message> Messages { get { return _Messages; } }
        public string Username { get; set; }
        public string Draft { get; set; } = string.Empty; // Черновик сообщения

        public Contact()
        {
            _Messages = new ObservableCollection<Message>();
        }
        public Contact(string username, string name)
        {
            _Name = name;
            Username = username;
            _Messages = new ObservableCollection<Message>();
        }
        public Contact(string username, string name, ObservableCollection<Message> messages)
        {
            _Name = name;
            Username = username;
            _Messages = messages;
        }

        public void ChangeName(string name)
        {
            _Name = name;
        }

        public string GetAllMeseges()
        {
            string AllMessege = string.Empty;
            foreach (var message in _Messages)
            {
                AllMessege += $"{message.ToString()}❂";
            }
            return AllMessege;
        }
    }
}
