using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TebegramServer
{
    public enum MessageType
    {
        Text,
        File        // Image убран - не используется
    }
    
    public class Message
    {
        private string _Sender;
        private string _Text;
        private string _Time;
        private MessageType _MessageType;

        public string Sender { get { return _Sender; } }
        public string Text { get { return _Text; } }
        public string Time { get { return _Time; } }
        public MessageType MessageType { get { return _MessageType; } }

        public Message(string sender, string text, string time, MessageType messageType = MessageType.Text)
        {
            _Sender = sender;
            _Text = text;
            _Time = time;
            _MessageType = messageType;
        }
    }
}
