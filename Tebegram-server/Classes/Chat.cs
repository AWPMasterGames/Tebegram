using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace TebegramServer.Classes
{
    public class Chat
    {
        private int _Id;
        private string _Name;
        private ObservableCollection<Message> _Messages;
        public int Id { get { return _Id; } }
        public string Name { get { return _Name; } }
        public User Owner { get; set; }
        public List<User> Members { get; set; }
        public ObservableCollection<Message> Messages { get { return _Messages; } }
        public string Avatar { get; set; }
        public bool IsGroup { get; set; }

        public Chat(int id, string name, bool isGroup, string avatar, User owner, List<User> members, ObservableCollection<Message> messages)
        {
            _Id = id;
            _Name = name;
            _Messages = messages;
            IsGroup = isGroup;
            Owner = owner;
            Members = members;
            Avatar = avatar;
        }

        // ── Перенос из main-dev (коммит 311bff0), с исправлением ────────────────
        // Формат: Id▫Name▫OwnerId▫MemberId,MemberId,▫IsGroup▫Avatar.
        // Пока протоколом не используется - задел под миграцию на групповые чаты.
        public override string ToString()
        {
            // В оригинале условие было ПЕРЕПУТАНО (Owner == null давал Owner?.Id,
            // а живой владелец - литерал "None"). Здесь - как задумано.
            string owner = Owner != null ? $"{Owner.Id}" : "None";
            string membersId = string.Empty;
            foreach (User u in Members)
            {
                membersId += $"{u.Id},";
            }
            return $"{Id}▫{Name}▫{owner}▫{membersId}▫{IsGroup}▫{Avatar}";
        }

        /// <summary>История чата одной строкой (сообщения через ❂) - как у Contact.</summary>
        public string GetAllMeseges()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var message in Messages)
            {
                sb.Append(message.ToString()).Append('❂');
            }
            return sb.ToString();
        }
    }
}
