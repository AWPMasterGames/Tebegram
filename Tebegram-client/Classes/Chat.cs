using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tebegrammmm.Classes
{
    public class Chat
    {
        private int _Id;
        private string _Name;
        private ObservableCollection<Message> _Messages;
        public int Id { get { return _Id; } }
        public string Name { get { return _Name; } }
        public ObservableCollection<Message> Messages { get { return _Messages; } }
        public string Avatar { get; set; }
        public bool IOwner { get; set; }

    }
}
