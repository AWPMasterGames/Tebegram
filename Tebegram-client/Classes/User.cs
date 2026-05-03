using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using Tebegrammmm.Classes;
using Tebegrammmm.Data;

namespace Tebegrammmm
{
    public class User : INotifyPropertyChanged
    {
        private static readonly HttpClient _http = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (m, c, ch, e) => true
        });
        private int _Id;
        private string _Login;
        private string _Password;
        private string _Name;
        private string _Avatar;

        public event PropertyChangedEventHandler PropertyChanged;

        public int Id { get { return _Id; } }
        public string Avatar
        {
            get => _Avatar;
            set
            {
                _Avatar = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Avatar)));
            }
        }
        public string Login { get { return _Login; } }
        public string Name { get { return _Name; } }

        public string Username { get; set; }

        public ObservableCollection<ChatFolder> ChatsFolders { get; set; }
        public ObservableCollection<Contact> Contacts { get { return ChatsFolders[0].Contacts; } set { ChatsFolders[0].Contacts = value; } }

        public bool InCall { get; set; }
        public int SelectedDeviceNum{ get; set; }

        public User(int id, string login, string password, string name, string username, ObservableCollection<ChatFolder> chatsFolders, string avatar)
        {
            _Id = id;
            Avatar = avatar;        // временный URL из ответа логина
            _Login = login;
            _Password = password;
            _Name = name;
            Username = username;
            ChatsFolders = chatsFolders;
            LoadAvatarFromServer();  // сразу запрашиваем канонический URL (→ кэш)
        }

        /// <summary>
        /// Загружает URL аватарки через /avatarsFileName/{id} — тот же формат,
        /// что Contact.GetUserAvatar(), гарантирует совпадение ключа кэша.
        /// </summary>
        private async void LoadAvatarFromServer()
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get,
                    $"{ServerData.ServerAdress}/avatarsFileName/{_Id}");
                using var resp = await _http.SendAsync(req);
                string fileName = (await resp.Content.ReadAsStringAsync()).Trim();
                if (!string.IsNullOrEmpty(fileName))
                    Avatar = $"{ServerData.ServerAdress}/avatars/{fileName}";
            }
            catch (Exception ex)
            {
                Log.Save($"[User.LoadAvatarFromServer] {ex.GetType().Name}: {ex.Message}");
            }
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
