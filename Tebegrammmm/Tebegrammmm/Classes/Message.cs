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
    

    public enum MessageStatus
    {
        Sent,      // Доставлено
        Pending,   // Не доставлено (серый фон)
        Failed     // Ошибка отправки
    }
    public class Message
    {
        private string _Sender;
        private string _Reciver;
        private string _Text;
        private string _Time;

        private MessageType _MessageType;
        private string _FilePath;
        private string _ServerAdress;
        private MessageStatus _Status;
        public string Sender { get { return _Sender; } }
        public string Reciver { get { return _Reciver; } }
        public string Text { get { return _Text; } }
        public string Time { get { return _Time; } }
        public MessageType MessageType { get { return _MessageType; } }
        public string ServerAdress { get { return _ServerAdress; } }
        public string Message_FilePath { get { return _FilePath; } }
        public MessageStatus Status { get { return _Status; } set { _Status = value; } }

        public Message(string sender, string reciver, string text, string time, MessageType messageType = MessageType.Text, string serverAdress = null, string filePath = null)
        {
            _Sender = sender;
            _Reciver = reciver;
            _Text = text;
            _Time = time;
            _MessageType = messageType;
            _ServerAdress = serverAdress;
            _FilePath = filePath;
            _Status = MessageStatus.Sent; // По умолчанию
        }
        public override string ToString()
        {
            return $"{Sender}▫{Reciver}▫{MessageType}▫{Time}▫{ServerAdress}▫{Text}";
        }
    }
}
