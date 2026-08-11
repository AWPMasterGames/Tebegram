using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tebegrammmm.Classes;

namespace Tebegrammmm
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
            Contacts = new ObservableCollection<Contact>();
            Chats = new ObservableCollection<Chat>();
            this._Icon = icon;
            this._IsCanRedact = isCanRedact;
        }

        public ChatFolder(string folderName, ObservableCollection<Contact> contacts, string icon = "📁", bool isCanRedact = true)
        {
            this._Icon = icon;
            this._FolderName = folderName;
            this._IsCanRedact = isCanRedact;
            Contacts = contacts == null? new ObservableCollection<Contact>() : contacts;
            // Chats инициализируем ВСЕГДА: раньше в этом конструкторе он оставался
            // null, и любое обращение к folder.Chats падало бы NullReference'ом
            Chats = new ObservableCollection<Chat>();
        }

        // ── Задел под групповые чаты (перенос из main-dev, коммит 311bff0) ──────
        // Конструктор «папка из чатов». ВАЖНО: Contacts тоже инициализируем - в
        // исходной версии main-dev он оставался null, и весь ТЕКУЩИЙ код, живущий
        // на folder.Contacts (список чатов, вход, папки), упал бы при первом же
        // использовании такой папки. Пока конструктором никто не пользуется - 
        // он ждёт миграции на протокол с ChatId (см. roadmap, Этап 23 п.5).
        public ChatFolder(string folderName, ObservableCollection<Chat> chats, string icon = "📁", bool isCanRedact = true)
        {
            this._Icon = icon;
            this._FolderName = folderName;
            this._IsCanRedact = isCanRedact;
            Chats = chats == null ? new ObservableCollection<Chat>() : chats;
            Contacts = new ObservableCollection<Contact>();
        }

        public void AddContact(Contact contact)
        {
            Contacts.Add(contact);
        }
        public void AddChat(Chat chat)
        {
            Chats.Add(chat);
        }
        public void RemoveContact(Contact contact)
        {
            Contacts.Remove(contact);
        }
        public void RemoveChat(Chat chat)
        {
            Chats.Remove(chat);
        }
        
        public void ChangeFolderName(string folderName)
        {
            _FolderName = folderName;
        }
    }
}
