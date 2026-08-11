namespace TebegramServer
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
        private string _Reciver;
        private string _Text;
        private string _Time;

        private MessageType _MessageType;
        private string _FilePath;
        private string _ServerAdress;
        public string Sender { get { return _Sender; } }
        public string Reciver { get { return _Reciver; } }
        public string Text { get { return _Text; } }
        public string Time { get { return _Time; } }
        public MessageType MessageType { get { return _MessageType; } }
        public string ServerAdress { get { return _ServerAdress; } }

        public Message(string sender,string reciver, string text, string time, MessageType messageType = MessageType.Text, string serverAdress = null)
        {
            _Sender = sender;
            _Reciver = reciver;
            _Text = text;
            _Time = time;
            _MessageType = messageType;
            _ServerAdress = serverAdress;
        }
        // В версии протокола 2 добавляется поле ChatId, и в ToString оно идёт первым:
        // $"{ChatId}▫{Sender}▫…". Формат меняется одновременно с клиентами, иначе у
        // выпущенных версий нарушается разбор. Затрагиваются Classes/Message.ToString
        // и MessengerWindow.AddMessageToUser в клиенте Windows, parseMessage и
        // buildRaw в docs/app.js.
        //
        // В поле Sender передаётся именно логин. Подстановка отображаемого имени
        // через UsersData.FindUserByUsername(Sender).Name недопустима: клиенты ищут
        // по этому полю контакт, а для удалённого пользователя вызов даёт исключение.
        public override string ToString()
        {
            return $"{Sender}▫{Reciver}▫{MessageType}▫{Time}▫{ServerAdress}▫{Text}";
        }
    }
}
