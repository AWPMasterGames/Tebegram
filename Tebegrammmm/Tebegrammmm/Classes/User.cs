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

        public string Username { get; set; }

        public ObservableCollection<ChatFolder> ChatsFolders { get; set; }

        public User(int id, string login, string password, string name, string username, ObservableCollection<ChatFolder> chatsFolders)
        {
            _Id = id;
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
    }
}
