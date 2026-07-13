using System.Collections.ObjectModel;
using TebegramServer.Classes;
namespace TebegramServer
{
    public class ChatFolder
    {
        private int _Id;
        private string _Icon;
        private string _FolderName;
        private bool _IsCanRedact;

        public int Id { get { return _Id; } }
        public string Icon { get { return _Icon; } }
        public string FolderName { get { return _FolderName; } }
        public string ChatsCount { get { return Contacts.Count.ToString(); } }
        public bool IsCanRedact { get { return _IsCanRedact; } }

        public ObservableCollection<Contact> Contacts;
        public ObservableCollection<Chat> Chats;

        public ChatFolder(string icon = "📁", bool isCanRedact = true)
        {
            _Id = new Random().Next(0,20000000);
            _FolderName = "Новая папка";
            Contacts = new ObservableCollection<Contact>();
            Chats = new ObservableCollection<Chat>();
            this._Icon = icon;
            this._IsCanRedact = isCanRedact;
        }

        public ChatFolder(string folderName, ObservableCollection<Contact> contacts, string icon = "📁", bool isCanRedact = true)
        {
            _Id = new Random().Next(0,20000000);
            this._Icon = icon;
            this._FolderName = folderName;
            this.Contacts = contacts;
            this._IsCanRedact = isCanRedact;
            Chats = new ObservableCollection<Chat>();
            if (contacts == null) Contacts = new ObservableCollection<Contact>();
            else if(contacts !=null) Contacts = contacts;
        }

        public ChatFolder(string folderName, ObservableCollection<Chat> chats, string icon = "📁", bool isCanRedact = true)
        {
            _Id = new Random().Next(0, 20000000);
            this._Icon = icon;
            this._FolderName = folderName;
            this.Chats = chats;
            this._IsCanRedact = isCanRedact;
            if (chats == null) Chats = new ObservableCollection<Chat>();
            else if (chats != null) Chats = chats;
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
