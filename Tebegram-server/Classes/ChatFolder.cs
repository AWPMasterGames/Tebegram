using System.Collections.ObjectModel;
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
        // Задел под групповые чаты (перенос из main-dev, коммит 311bff0):
        // коллекция пока пуста - наполнение начнётся после миграции протокола
        public ObservableCollection<Classes.Chat> Chats;

        public ChatFolder(string icon = "📁", bool isCanRedact = true)
        {
            _Id = new Random().Next(0,20000000);
            _FolderName = "Новая папка";
            Contacts = new ObservableCollection<Contact>();
            Chats = new ObservableCollection<Classes.Chat>();
            this._Icon = icon;
            this._IsCanRedact = isCanRedact;
        }

        public ChatFolder(string folderName, ObservableCollection<Contact> contacts, string icon = "📁", bool isCanRedact = true)
        {
            _Id = new Random().Next(0,20000000);
            this._Icon = icon;
            this._FolderName = folderName;
            this._IsCanRedact = isCanRedact;
            Contacts = contacts == null ? new ObservableCollection<Contact>() : contacts;
            // Chats инициализируем ВСЕГДА, иначе любое обращение к folder.Chats
            // (например, при будущей сериализации) падало бы NullReference'ом
            Chats = new ObservableCollection<Classes.Chat>();
        }

        // Конструктор «папка из чатов» (из main-dev). ВАЖНО: Contacts тоже
        // инициализируем - в оригинале он оставался null, и весь ТЕКУЩИЙ код
        // (ToClientSend, автосохранение, /messages) упал бы на такой папке.
        public ChatFolder(string folderName, ObservableCollection<Classes.Chat> chats, string icon = "📁", bool isCanRedact = true)
        {
            _Id = new Random().Next(0, 20000000);
            this._Icon = icon;
            this._FolderName = folderName;
            this._IsCanRedact = isCanRedact;
            Chats = chats == null ? new ObservableCollection<Classes.Chat>() : chats;
            Contacts = new ObservableCollection<Contact>();
        }

        public void AddContact(Contact contact)
        {
            Contacts.Add(contact);
        }
        public void AddChat(Classes.Chat chat)
        {
            Chats.Add(chat);
        }
        public void RemoveContact(Contact contact)
        {
            Contacts.Remove(contact);
        }
        public void RemoveChat(Classes.Chat chat)
        {
            Chats.Remove(chat);
        }
        
        public void ChangeFolderName(string folderName)
        {
            _FolderName = folderName;
        }
    }
}
