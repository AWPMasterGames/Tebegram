using Microsoft.VisualBasic.ApplicationServices;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using Tebegrammmm.Data;

namespace Tebegrammmm.Classes
{
    public class Chat
    {
        static HttpClient httpClient = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (m, c, ch, e) => true
        });


        private int _Id;
        private string _Name;
        private ObservableCollection<Message> _Messages;
        private ObservableCollection<Contact> Members;
        public int Id { get { return _Id; } }
        public string Name { get { return _Name; } }
        public ObservableCollection<Message> Messages { get { return _Messages; } }
        public string Avatar { get; set; }
        public bool IsGroup { get; set; }
        public bool IOwner { get; set; }

        public Chat(int id, string name, bool isGroup, string avatar, bool iOwner)
        {
            _Id = id;
            _Name = name;
            IsGroup = isGroup;
            IOwner = iOwner;
            Avatar = $"{ServerData.ServerAdress}/avatars/{avatar}";
            Members = new ObservableCollection<Contact>();
            _Messages = new ObservableCollection<Message>();
            GetUserAvatar();
        }
        public Chat(int id, string name, bool isGroup, string avatar, bool iOwner, ObservableCollection<Contact> members, ObservableCollection<Message> messages)
        {
            _Id = id;
            _Name = name;
            _Messages = messages;
            IsGroup = isGroup;
            IOwner = iOwner;
            Members = members;
            Avatar = $"{ServerData.ServerAdress}/avatars/{avatar}";
            GetUserAvatar();
        }

        private async void GetUserAvatar()
        {
            if (IsGroup) return;
            int UserId = 0;
            foreach (var member in Members)
            {
                if(member.UserId != UserData.User.Id) UserId = member.UserId;
            }
            if(UserId == 0) return;
            try
            {
                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{ServerData.ServerAdress}/avatarsFileName/{UserId}");
                using HttpResponseMessage response = await httpClient.SendAsync(request);
                string content = await response.Content.ReadAsStringAsync();
                Avatar = $"{ServerData.ServerAdress}/avatars/{content}";
            }
            catch (Exception ex)
            {
                Log.Save($"[Contact.GetUserAvatar] {ex.GetType().Name}: {ex.Message}");
            }
        }

    }
}
