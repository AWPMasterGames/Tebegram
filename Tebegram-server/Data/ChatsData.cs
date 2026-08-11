using System.Collections.ObjectModel;
using System.Text.Json;
using TebegramServer.Classes;
using TebegramServer.Controllers;

namespace TebegramServer.Data
{
    /// <summary>
    /// Сохранение и загрузка чатов, прежде всего групповых.
    ///
    /// Чаты хранятся в памяти в ChatsController.Chats, тогда как в Users.json
    /// сериализуются только контакты с перепиской. Без отдельного файла группы
    /// пропадали при каждом перезапуске сервера. Схема повторяет UsersData: файл
    /// рядом с исполняемым, полная перезапись, резервная копия.
    ///
    /// Порядок загрузки при старте существенен: сначала UsersData, затем чаты,
    /// поскольку участники разыскиваются по Id среди уже загруженных пользователей.
    /// </summary>
    public static class ChatsData
    {
        private static string FilePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Chats.json");

        private static string BackupPath => Path.ChangeExtension(FilePath, ".backup.json");

        public static void Save()
        {
            try
            {
                // Снимок словаря: во время сохранения другой поток может создать чат
                List<Chat> chats;
                lock (ChatsController.Chats)
                {
                    chats = ChatsController.Chats.Values.ToList();
                }

                // Личные чаты не сохраняем: их переписка уже лежит в контактах
                // пользователей (Users.json), и при отправке они пересоздаются
                // сами через CheckIsExist. Дублировать историю нет смысла.
                var groups = chats.Where(c => c.IsGroup).ToList();
                if (groups.Count == 0) return;

                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

                var data = groups.Select(chat => new ChatRecord
                {
                    Id = chat.Id,
                    Name = chat.Name,
                    IsGroup = chat.IsGroup,
                    Avatar = chat.Avatar ?? "",
                    OwnerId = chat.Owner?.Id ?? 0,
                    MemberIds = chat.Members.ToList().Select(m => m.Id).ToList(),
                    Messages = chat.Messages.ToList().Select(m => new MessageRecord
                    {
                        Sender = m.Sender,
                        Recipient = m.Reciver,
                        Text = m.Text,
                        Time = m.Time,
                        MessageType = m.MessageType.ToString(),
                        ServerAdress = m.ServerAdress ?? "",
                    }).ToList(),
                }).ToList();

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                File.WriteAllText(FilePath, JsonSerializer.Serialize(data, options));

                try { File.Copy(FilePath, BackupPath, overwrite: true); }
                catch { /* бэкап не критичен */ }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Ошибка сохранения чатов: {ex.Message}");
            }
        }

        /// <summary>Вызывать ПОСЛЕ загрузки пользователей - участники ищутся по Id.</summary>
        public static void Load()
        {
            try
            {
                string path = FilePath;
                if (!File.Exists(path))
                {
                    if (File.Exists(BackupPath))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                        File.Copy(BackupPath, path);
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Chats.json не найден - восстановлен из резервной копии");
                    }
                    else
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Файл чатов не найден - групп пока нет");
                        return;
                    }
                }

                var records = JsonSerializer.Deserialize<List<ChatRecord>>(File.ReadAllText(path));
                if (records == null) return;

                int restored = 0;
                foreach (ChatRecord record in records)
                {
                    // Участники, которых уже нет в базе, просто пропускаем
                    List<User> members = record.MemberIds
                        .Select(UsersData.FindUserById)
                        .Where(u => u != null)
                        .Select(u => u!)
                        .ToList();
                    if (members.Count == 0) continue;

                    var messages = new ObservableCollection<Message>();
                    foreach (MessageRecord m in record.Messages)
                    {
                        var type = Enum.TryParse<MessageType>(m.MessageType, out var t) ? t : MessageType.Text;
                        string? server = string.IsNullOrEmpty(m.ServerAdress) ? null : m.ServerAdress;
                        messages.Add(new Message(m.Sender, m.Recipient, m.Text, m.Time, type, server));
                    }

                    User? owner = record.OwnerId > 0 ? UsersData.FindUserById(record.OwnerId) : null;
                    var chat = new Chat(record.Id, record.Name, record.IsGroup, record.Avatar, owner, members, messages);

                    ChatsController.Chats[chat.Id] = chat;

                    // Возвращаем чат в списки участников - иначе он есть на сервере,
                    // но пользователь про него «не знает»
                    foreach (User member in members)
                    {
                        if (!member.Chats.Contains(chat.Id)) member.AddChat(chat.Id);
                    }
                    restored++;
                }

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Загружено чатов: {restored}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Ошибка загрузки чатов: {ex.Message}");
            }
        }

        // ── Формат файла ────────────────────────────────────────────────────
        private class ChatRecord
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public bool IsGroup { get; set; }
            public string Avatar { get; set; } = "";
            public int OwnerId { get; set; }
            public List<int> MemberIds { get; set; } = new();
            public List<MessageRecord> Messages { get; set; } = new();
        }

        private class MessageRecord
        {
            public string Sender { get; set; } = "";
            public string Recipient { get; set; } = "";
            public string Text { get; set; } = "";
            public string Time { get; set; } = "";
            public string MessageType { get; set; } = "Text";
            public string ServerAdress { get; set; } = "";
        }
    }
}
