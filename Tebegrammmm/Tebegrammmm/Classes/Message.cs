using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tebegrammmm
{
    public enum MessageType
    {
        Text,
        Image,
        File
    }
    public class Message
    {
        private string _Sender;
        private string _Text;
        private string _Time;

        private MessageType _MessageType;
        private string _FilePath;
        private string _ServerAdress;
        public string Sender { get { return _Sender; } }
        public string Text { get { return _Text; } }
        public string Time { get { return _Time; } }
        public MessageType MessageType { get { return _MessageType; } }
        public string ServerAdress { get { return _ServerAdress; } }

        public Message(string sender, string text, string time, MessageType messageType = MessageType.Text, string serverAdress = null)
        {
            _Sender = sender;
            _Text = text;
            _Time = time;
            _MessageType = messageType;
            _ServerAdress = serverAdress;
        }
    }
}
