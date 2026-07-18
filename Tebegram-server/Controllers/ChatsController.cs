using System.Collections.ObjectModel;
using System.Net.WebSockets;
using System.Text;
using TebegramServer.Classes;
using TebegramServer.Data;
using static System.Net.Mime.MediaTypeNames;

namespace TebegramServer.Controllers
{
    public static class ChatsController
    {
        public static Dictionary<int, Chat> Chats = new Dictionary<int, Chat>();


        public static int CreateChat(List<User> members)
        {
            Chat chat;
            if (members.Count < 3)
            {
                chat = new Chat(1000000 + new Random().Next(int.MaxValue - 1000001), "", false, "", null, members, new ObservableCollection<Message>());
            }
            else
            {
                string gName = string.Empty;
                for (int i = 0; i < members.Count -1 ; i++)
                {
                    gName += $"{members[i].Name}, ";
                }
                gName += $"{members[members.Count - 1].Name}";
                chat = new Chat(1000000 + new Random().Next(int.MaxValue - 1000001), gName, true, "", members[0], members, new ObservableCollection<Message>());
            }

            Chats.Add(chat.Id, chat);
            foreach (User user in members)
            {
                user.AddChat(chat.Id);
            }

            return chat.Id;

        }


        public static async void SendMessage(int chatId, string messageD)
        {
            string[] messageData = messageD.Split('▫');
            Message message = null;
            if (messageData[3] == "Text")
            {
                string text = messageData[6];
                for (int i = 7; i < messageData.Length; i++)
                {
                    text += messageData[i];
                }
                message = new Message(int.Parse(messageData[0]), messageData[1], messageData[2], text, messageData[4]);
            }
            else if (messageData[3] == "File")
            {
                message = new Message(int.Parse(messageData[0]), messageData[1], messageData[2], messageData[6], messageData[4], MessageType.File, messageData[5]);
            }
            Chats[chatId].Messages.Add(message);

            foreach (User user in Chats[chatId].Members)
            {
                foreach (WebSocket session in user.ChatsSessions)
                {
                    if (session.State == WebSocketState.Open)
                    {
                        Console.WriteLine($"Send to user: {user.Username} | message: {message.ToString()}");
                        var arraySegment = new ArraySegment<byte>(Encoding.UTF8.GetBytes($"addMessage▫$▫{message.ToString()}"));
                        await session.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                }
            }
        }

        public static int CheckIsExist(int chatId, User user, string receiver)
        {
            if (!ContainsChat(chatId))
            {
                List<User> members = new List<User>();
                members.Add(user);
                members.Add(UsersData.FindUserByUsername(receiver));
                chatId = CreateChat(members);
            }
            return chatId;
        }











        public static bool ContainsChat(int id)
        {
            return Chats.ContainsKey(id);
        }
    }
}
