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

    }
}
