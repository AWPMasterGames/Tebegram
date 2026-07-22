using System.Collections.ObjectModel;
using System.Net.WebSockets;
using System.Text;
using TebegramServer.Classes;
using TebegramServer.Data;

namespace TebegramServer.Controllers
{
    public static class ChatsController
    {
        public static Dictionary<int, Chat> Chats = new Dictionary<int, Chat>();
        private static readonly object _lock = new object();
        private static readonly Random _random = new Random();

        public static int CreateChat(List<User> members)
        {
            lock (_lock)
            {
                // Убираем дубли — для чата с собой members = [user, user] превращается в [user]
                members = members.Distinct().ToList();

                // Генерируем Id, пока не найдём свободный — раньше случайный Id мог совпасть и Add кидал исключение
                int id;
                do
                {
                    id = 1000000 + _random.Next(int.MaxValue - 1000001);
                } while (Chats.ContainsKey(id));

                Chat chat;
                if (members.Count < 3)
                {
                    // Личный чат (или чат с собой — «Избранное»)
                    chat = new Chat(id, "", false, "", null, members, new ObservableCollection<Message>());
                }
                else
                {
                    // Группа (перенос из main-dev, коммит 64bc1ab): имя по умолчанию —
                    // перечисление имён участников, владелец — создатель (первый в списке)
                    string gName = string.Join(", ", members.Select(m => m.Name));
                    chat = new Chat(id, gName, true, "", members[0], members, new ObservableCollection<Message>());
                }

                Chats.Add(chat.Id, chat);
                foreach (User user in members)
                {
                    user.AddChat(chat.Id);
                }

                if (chat.IsGroup)
                {
                    SendGroupsToUsers(chat);
                }

                return chat.Id;
            }
        }

        private static async void SendGroupsToUsers(Chat chat)
        {
            string owner = chat.Owner != null ? $"{chat.Owner?.Id}" : "None";
            foreach (User user in chat.Members)
            {
                foreach (WebSocket session in user.ChatsSessions)
                {
                    if (session.State == WebSocketState.Open)
                    {
                        string ServerMessage = $"addChat▫$▫{chat.Id}&{chat.Name}&{chat.IsGroup}&{chat.Avatar}&{owner}";
                        var arraySegment = new ArraySegment<byte>(Encoding.UTF8.GetBytes($"addChat▫$▫{ServerMessage}"));
                        await session.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                }
            }
            Console.WriteLine("Group Created");
        }

        public static async Task SendMessage(int chatId, string messageD)
        {
            Chat? chat;
            lock (_lock)
            {
                if (!Chats.TryGetValue(chatId, out chat)) return;
            }

            string[] messageData = messageD.Split('▫');
            Message? message = null;
            if (messageData.Length >= 6 && messageData[2] == "Text")
            {
                // Текст может содержать ▫ — склеиваем хвост обратно с разделителем
                string text = string.Join('▫', messageData.Skip(5));
                message = new Message(messageData[0], messageData[1], text, messageData[3]);
            }
            else if (messageData.Length >= 6 && messageData[2] == "File")
            {
                message = new Message(messageData[0], messageData[1], messageData[5], messageData[3], MessageType.File, messageData[4]);
            }
            if (message == null) return;

            chat.Messages.Add(message);

            foreach (User user in chat.Members.ToList())
            {
                // Снимок списка сессий: коллекция может меняться из других потоков во время рассылки
                foreach (WebSocket session in user.ChatsSessions.ToList())
                {
                    if (session.State == WebSocketState.Open)
                    {
                        try
                        {
                            //Console.WriteLine($"Send to user: {user.Username} | message: {message}");
                            // ПЕРЕХОД НА ChatId: в v2 сообщение оборачивается в конверт
                            // $"addMessage▫$▫{message}" (win-клиент и веб УЖЕ понимают
                            // оба формата — см. GetMessage / ws.onmessage), а само
                            // message.ToString() начнёт включать ChatId первым полем.
                            // Включать конверт можно только когда все клиенты обновятся.
                            var arraySegment = new ArraySegment<byte>(Encoding.UTF8.GetBytes(message.ToString()));
                            await session.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
                        }
                        catch (WebSocketException)
                        {
                            // Сокет умер между проверкой State и отправкой — просто пропускаем
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Возвращает Id чата для отправки. Если чат с таким Id не существует,
        /// ищет существующий личный чат между этими двумя пользователями
        /// (раньше на каждое сообщение создавался новый чат, т.к. клиент всегда шлёт chatId=0).
        /// Если и его нет — создаёт новый. Возвращает -1, если получатель не найден.
        /// ПЕРЕХОД НА ChatId: когда клиенты начнут слать реальный chatId в SEND,
        /// весь поиск по username здесь станет фолбэком для старых клиентов —
        /// основной путь сведётся к первой проверке ContainsChat(chatId).
        /// </summary>
        public static int CheckIsExist(int chatId, User user, string receiver)
        {
            lock (_lock)
            {
                if (ContainsChat(chatId)) return chatId;
            }

            User? receiverUser = UsersData.FindUserByUsername(receiver);
            if (receiverUser == null) return -1;

            bool isSelfChat = ReferenceEquals(user, receiverUser);

            lock (_lock)
            {
                foreach (Chat chat in Chats.Values)
                {
                    if (chat.IsGroup) continue;
                    if (isSelfChat)
                    {
                        // Чат с собой — ровно один участник (я). Иначе совпал бы любой мой чат.
                        if (chat.Members.Count == 1 && chat.Members[0] == user) return chat.Id;
                    }
                    else if (chat.Members.Contains(user) && chat.Members.Contains(receiverUser))
                    {
                        return chat.Id;
                    }
                }
            }

            return CreateChat(new List<User> { user, receiverUser });
        }

        public static bool ContainsChat(int id)
        {
            lock (_lock)
            {
                return Chats.ContainsKey(id);
            }
        }
    }
}
