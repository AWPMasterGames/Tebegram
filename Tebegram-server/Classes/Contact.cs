using System.Collections.ObjectModel;
namespace TebegramServer
{
    public class Contact
    {
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
        }
        public Contact(int id, string username, string name, ObservableCollection<Message> messages)
        {
            _UserId = id;
            _Name = name;
            Username = username;
            _Messages = messages;
        }

        public void ChangeName(string name)
        {
            _Name = name;
        }
        public override string ToString()
        {
            return $"{UserId}▫{Name}▫{Username}";
        }
        public string GetAllMeseges()
        {
            string AllMessege = string.Empty;
            foreach (var message in _Messages)
            {
                AllMessege += $"{message.ToString()}❂";
            }
            return AllMessege;
        }
    }
}
