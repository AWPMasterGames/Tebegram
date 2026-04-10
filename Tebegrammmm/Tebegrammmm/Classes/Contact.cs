using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Tebegrammmm.Data;

namespace Tebegrammmm
{
    public class Contact
    {
        static HttpClient httpClient = new HttpClient();

        private int _UserId;
        private string _Name = string.Empty;
        private ObservableCollection<Message> _Messages;
        public int UserId { get { return _UserId; } }
        public string Name { get { return _Name; } }
        public ObservableCollection<Message> Messages { get { return _Messages; } }
        public string Username { get; set; }
        public string Avatar { get; set; }
        public string Draft { get; set; } = string.Empty; // Черновик сообщения

        public Contact()
        {
            _Messages = new ObservableCollection<Message>();
        }
        public Contact(int id, string username, string name)
        {
            _UserId = id;
            _Name = name;
            Username = username;
            _Messages = new ObservableCollection<Message>();
            GetUserAvatar();
        }
        public Contact(int id, string username, string name, ObservableCollection<Message> messages)
        {
            _UserId = id;
            _Name = name;
            Username = username;
            _Messages = messages;
            GetUserAvatar();
        }

        public void ChangeName(string name)
        {
            _Name = name;
        }

        public void ChangeUsername(string username)
        {
            Username = username;
        }

        private async void GetUserAvatar()
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{ServerData.ServerAdress}/avatarsFileName/{UserId}");
            using HttpResponseMessage response = await httpClient.SendAsync(request);
            string content = await response.Content.ReadAsStringAsync();
            Avatar = $"{ServerData.ServerAdress}/avatars/{content}";
        }
    }
}
