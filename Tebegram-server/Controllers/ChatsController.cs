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

        /// <summary>Конверт команды «создан чат» в WS-канале.</summary>
        public const string AddChatEnvelope = "addChat▫$▫";

        public const string RemoveChatEnvelope = "removeChat▫$▫";

        /// <summary>Конверт сообщения ГРУППОВОГО чата (внутри - ChatId первым полем).</summary>
        public const string AddMessageEnvelope = "addMessage▫$▫";

        /// <summary>
        /// Одна строка описания чата для клиента:
        /// id&amp;имя&amp;группа?&amp;аватар&amp;владелец&amp;участники_через_запятую.
        /// Используется и в уведомлении addChat, и в ответе /Chats/{userId}, чтобы
        /// формат не разъезжался между двумя местами.
        /// </summary>
        public static string ChatToLine(Chat chat)
        {
            string owner = chat.Owner != null ? $"{chat.Owner.Id}" : "None";
            string members = string.Join(",", chat.Members.Select(m => m.Id));
            return $"{chat.Id}&{chat.Name}&{chat.IsGroup}&{chat.Avatar}&{owner}&{members}";
        }

        /// <param name="groupName">
        /// Название группы из интерфейса. Пусто - соберём из имён участников
        /// (так вело себя старое поведение).
        /// </param>
        public static int CreateChat(List<User> members, string groupName = "")
        {
            Chat chat;

            lock (_lock)
            {
                // Убираем дубли - для чата с собой members = [user, user] превращается в [user]
                members = members.Distinct().ToList();

                // Генерируем Id, пока не найдём свободный - раньше случайный Id мог совпасть и Add кидал исключение
                int id;
                do
                {
                    id = 1000000 + _random.Next(int.MaxValue - 1000001);
                } while (Chats.ContainsKey(id));

                if (members.Count < 3)
                {
                    // Личный чат (или чат с собой - «Избранное»)
                    chat = new Chat(id, "", false, "", null, members, new ObservableCollection<Message>());
                }
                else
                {
                    // Группа: имя из интерфейса, а если его не передали - перечисление имён участников
                    string gName = string.IsNullOrWhiteSpace(groupName)
                        ? string.Join(", ", members.Select(m => m.Name))
                        : groupName.Trim();
                    chat = new Chat(id, gName, true, "", members[0], members, new ObservableCollection<Message>());
                }

                Chats.Add(chat.Id, chat);
                foreach (User user in members)
                {
                    user.AddChat(chat.Id);
                }
            }

            // Рассылка ВНЕ блокировки: раньше она запускалась внутри lock как async void,
            // и продолжение после await выполнялось уже вне лока, а любое исключение
            // в async void роняет процесс сервера целиком
            if (chat.IsGroup) _ = NotifyChatCreatedAsync(chat);

            return chat.Id;
        }

        // Task, а НЕ async void: исключение из async void не ловится вызывающим и
        // роняет весь процесс сервера. Теперь оно всплывает в await и обрабатывается.
        public static async Task DeleteChat(int chatId, User owner)
        {
            List<User> members;
            lock (_lock)
            {
                // TryGetValue, а не индексатор Chats[chatId]: на несуществующем или
                // уже удалённом id индексатор кидал KeyNotFoundException → краш сервера
                if (!Chats.TryGetValue(chatId, out Chat chat)) return;
                // Удалять группу может только её владелец
                if (chat.Owner != owner) return;

                // Снимок участников ДО удаления - по нему разошлём уведомление.
                // Всю правку структур делаем под тем же _lock, что и остальной
                // ChatsController, иначе рассылка/сохранение могут поймать полусостояние.
                members = chat.Members.ToList();
                Chats.Remove(chatId);
                foreach (User u in members) u.RemoveChat(chatId);
            }

            // Рассылку выносим ИЗ-под lock: держать блокировку через await нельзя.
            // Уведомляем всех участников (включая владельца) - у каждого чат
            // пропадёт из списка (см. клиент, HandleRemoveChat).
            byte[] payload = Encoding.UTF8.GetBytes($"{RemoveChatEnvelope}{chatId}");
            foreach (User u in members)
            {
                foreach (WebSocket session in u.ChatsSessions.ToList())
                {
                    if (session.State != WebSocketState.Open) continue;
                    try
                    {
                        await session.SendAsync(new ArraySegment<byte>(payload),
                            WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    catch (WebSocketException)
                    {
                        // Сокет умер между проверкой State и отправкой - пропускаем
                    }
                }
            }
        }

        /// <summary>
        /// Убирает участника из группы по его собственному желанию.
        ///
        /// В отличие от DeleteChat, действие доступно любому участнику и затрагивает
        /// только его: у остальных чат остаётся на месте. Ушедшему отправляется тот же
        /// конверт removeChat, что и при удалении группы, поэтому отдельная обработка
        /// на клиенте не нужна.
        ///
        /// Владелец тоже может выйти, право переходит к самому раннему из оставшихся:
        /// иначе группу стало бы некому удалить. Когда уходит последний участник,
        /// чат удаляется целиком.
        ///
        /// Оставшимся участникам уведомление не рассылается: протокол не описывает
        /// события «участник вышел», а повторный addChat клиент отбрасывает как
        /// дубликат. Состав группы у них обновится при следующем входе.
        /// </summary>
        public static async Task LeaveChat(int chatId, User user)
        {
            lock (_lock)
            {
                if (!Chats.TryGetValue(chatId, out Chat chat)) return;
                // Из личного чата выходить некуда: он определяется парой собеседников
                if (!chat.IsGroup) return;
                // Remove вернёт false, если запрос пришёл не от участника
                if (!chat.Members.Remove(user)) return;
                user.RemoveChat(chatId);

                if (chat.Members.Count == 0) Chats.Remove(chatId);
                else if (chat.Owner == user) chat.Owner = chat.Members[0];
            }

            // Рассылка вне lock: держать блокировку через await нельзя
            byte[] payload = Encoding.UTF8.GetBytes($"{RemoveChatEnvelope}{chatId}");
            foreach (WebSocket session in user.ChatsSessions.ToList())
            {
                if (session.State != WebSocketState.Open) continue;
                try
                {
                    await session.SendAsync(new ArraySegment<byte>(payload),
                        WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch (WebSocketException)
                {
                    // Сокет умер между проверкой State и отправкой - пропускаем
                }
            }
        }

        /// <summary>
        /// Сообщает участникам группы, что чат создан. Конверт добавляется РОВНО ОДИН
        /// раз (в исходной версии префикс клеился дважды, и клиенту приходило
        /// «addChat▫$▫addChat▫$▫…»; на клиенте это гасилось Replace - пара ошибок
        /// компенсировала друг друга, но любая односторонняя правка всё ломала).
        /// </summary>
        private static async Task NotifyChatCreatedAsync(Chat chat)
        {
            try
            {
                byte[] payload = Encoding.UTF8.GetBytes(AddChatEnvelope + ChatToLine(chat));

                // Снимки коллекций: во время рассылки участники и сессии могут меняться
                foreach (User user in chat.Members.ToList())
                {
                    foreach (WebSocket session in user.ChatsSessions.ToList())
                    {
                        if (session.State != WebSocketState.Open) continue;
                        try
                        {
                            await session.SendAsync(new ArraySegment<byte>(payload),
                                WebSocketMessageType.Text, true, CancellationToken.None);
                        }
                        catch (WebSocketException)
                        {
                            // Сокет умер между проверкой состояния и отправкой - пропускаем
                        }
                    }
                }
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Создан групповой чат {chat.Id} «{chat.Name}» ({chat.Members.Count} участн.)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Ошибка рассылки о создании чата: {ex.Message}");
            }
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
                // Текст может содержать ▫ - склеиваем хвост обратно с разделителем,
                // затем чистим. Оба клиента вызывают SanitizeMessage сами, но запрос
                // в обход клиента иначе доставит ❂ в историю чата.
                string text = Tebegram.Shared.UserValidation.SanitizeMessage(string.Join('▫', messageData.Skip(5)));
                if (text == null) return;
                message = new Message(messageData[0], messageData[1], text, messageData[3]);
            }
            else if (messageData.Length >= 6 && messageData[2] == "File")
            {
                message = new Message(messageData[0], messageData[1], messageData[5], messageData[3], MessageType.File, messageData[4]);
            }
            if (message == null) return;

            chat.Messages.Add(message);

            // Личный чат - СТАРЫЙ формат без конверта: его понимают все выпущенные
            // клиенты, ломать их нельзя. Группа - конверт с ChatId первым полем:
            // в 1:1 клиент определяет чат по собеседнику, а в группе отправитель
            // не говорит, куда класть сообщение, поэтому Id обязателен.
            string wire = chat.IsGroup
                ? $"{AddMessageEnvelope}{chat.Id}▫{message}"
                : message.ToString();
            byte[] payload = Encoding.UTF8.GetBytes(wire);

            foreach (User user in chat.Members.ToList())
            {
                // Снимок списка сессий: коллекция может меняться из других потоков во время рассылки
                foreach (WebSocket session in user.ChatsSessions.ToList())
                {
                    if (session.State == WebSocketState.Open)
                    {
                        try
                        {
                            await session.SendAsync(new ArraySegment<byte>(payload),
                                WebSocketMessageType.Text, true, CancellationToken.None);
                        }
                        catch (WebSocketException)
                        {
                            // Сокет умер между проверкой State и отправкой - просто пропускаем
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Возвращает Id чата для отправки сообщения.
        ///
        /// Если чат с указанным Id отсутствует, выполняется поиск личного чата между
        /// двумя пользователями. Поиск добавлен потому, что клиент всегда передаёт
        /// chatId=0 и на каждое сообщение создавался новый чат. Если подходящего
        /// чата нет, создаётся новый. Значение -1 означает, что получатель не найден.
        ///
        /// После перехода клиентов на передачу действительного chatId в команде SEND
        /// поиск по логину станет запасным путём для выпущенных ранее версий,
        /// а основным останется первая проверка ContainsChat(chatId).
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
                        // Чат с собой - ровно один участник (я). Иначе совпал бы любой мой чат.
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
