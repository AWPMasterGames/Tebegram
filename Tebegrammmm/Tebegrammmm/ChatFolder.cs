using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tebegrammmm
{
    public class ChatFolder
    {
        private string _Icon;
        private string _FolderName;
        private bool _IsCanRedact;

        public string Icon { get { return _Icon; } }
        public string FolderName { get { return _FolderName; } }
        public string ChatsCount { get { return Contacts.Count.ToString(); } }
        public bool IsCanRedact { get { return _IsCanRedact; } }

        public ObservableCollection<Contact> Contacts;

        public ChatFolder(string icon = "📁", bool isCanRedact = true)
        {
            Contacts = new ObservableCollection<Contact>();
            this._Icon = icon;
            this._IsCanRedact = isCanRedact;
        }

        public ChatFolder(string folderName, ObservableCollection<Contact> contacts, string icon = "📁", bool isCanRedact = true)
        {
            this._Icon = icon;
            this._FolderName = folderName;
            this.Contacts = contacts;
            this._IsCanRedact = isCanRedact;
            if(contacts == null) Contacts = new ObservableCollection<Contact>();
            else if(contacts !=null) Contacts = contacts;
        }

        public void AddContact(Contact contact)
        {
            Contacts.Add(contact);
        }
        public void RemoveContact(Contact contact)
        {
            Contacts.Remove(contact);
        }
        
        public void ChangeFolderName(string folderName)
        {
            _FolderName = folderName;
        }
    }
}
