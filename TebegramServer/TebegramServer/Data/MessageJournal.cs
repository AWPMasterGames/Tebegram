using System.Collections.ObjectModel;
using System.Text.Json;
using TebegramServer.Classes;

namespace TebegramServer.Data
{
    /// <summary>
    /// Класс для ведения журнала сообщений каждого пользователя
    /// Каждый пользователь записывает только свои собственные сообщения
    /// 
    /// ПРИНЦИП РАБОТЫ:
    /// 1. Когда пользователь отправляет сообщение, оно записывается только в ЕГО журнал
    /// 2. Каждый журнал содержит сообщения в строгом хронологическом порядке
    /// 3. Для получения диалога объединяются журналы двух пользователей
    /// 4. Это гарантирует, что порядок сообщений никогда не будет нарушен
    /// 
    /// СТРУКТУРА ФАЙЛОВ:
    /// - MessageJournal/{username}_journal.json - журнал каждого пользователя
    /// - В каждом файле только сообщения, отправленные этим пользователем
    /// </summary>
    public static class MessageJournal
    {
        private static readonly string JournalDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MessageJournal");

        static MessageJournal()
        {
            // Создаем директорию для журналов, если она не существует
            if (!Directory.Exists(JournalDirectory))
            {
                Directory.CreateDirectory(JournalDirectory);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Создана директория журналов сообщений: {JournalDirectory}");
            }
        }

        /// <summary>
        /// Добавляет сообщение в журнал отправителя
        /// Только отправитель записывает сообщение в свой журнал
        /// </summary>
        public static async Task AddMessageToJournal(string senderUsername, Message message)
        {
            try
            {
                string journalFile = Path.Combine(JournalDirectory, $"{senderUsername}_journal.json");
                
                List<MessageJournalEntry> journal = new List<MessageJournalEntry>();

                // Загружаем существующий журнал, если он есть
                if (File.Exists(journalFile))
                {
                    string existingContent = await File.ReadAllTextAsync(journalFile);
                    if (!string.IsNullOrEmpty(existingContent))
                    {
                        try
                        {
                            var existing = JsonSerializer.Deserialize<List<MessageJournalEntry>>(existingContent);
                            if (existing != null)
                            {
                                journal = existing;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Ошибка при загрузке журнала {senderUsername}: {ex.Message}");
                            journal = new List<MessageJournalEntry>();
                        }
                    }
                }

                // Создаем новую запись в журнале
                var journalEntry = new MessageJournalEntry
                {
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), // Точное время с миллисекундами
                    Sender = message.Sender,
                    Recipient = message.Reciver,
                    Text = message.Text,
                    MessageType = message.MessageType.ToString(),
                    TimeDisplay = message.Time, // Время для отображения
                    MessageString = message.ToString(),
                    SequenceNumber = journal.Count + 1 // Порядковый номер в журнале пользователя
                };

                // Добавляем в журнал
                journal.Add(journalEntry);

                // Сохраняем журнал с форматированием
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                string jsonContent = JsonSerializer.Serialize(journal, options);
                await File.WriteAllTextAsync(journalFile, jsonContent);

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Сообщение добавлено в журнал {senderUsername}: #{journalEntry.SequenceNumber} -> {message.Reciver}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Ошибка при записи в журнал {senderUsername}: {ex.Message}");
            }
        }

        /// <summary>
        /// Получает все сообщения пользователя из его журнала
        /// </summary>
        public static async Task<List<MessageJournalEntry>> GetUserJournal(string username)
        {
            try
            {
                string journalFile = Path.Combine(JournalDirectory, $"{username}_journal.json");
                
                if (!File.Exists(journalFile))
                {
                    return new List<MessageJournalEntry>();
                }

                string content = await File.ReadAllTextAsync(journalFile);
                if (string.IsNullOrEmpty(content))
                {
                    return new List<MessageJournalEntry>();
                }

                var journal = JsonSerializer.Deserialize<List<MessageJournalEntry>>(content);
                return journal ?? new List<MessageJournalEntry>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Ошибка при чтении журнала {username}: {ex.Message}");
                return new List<MessageJournalEntry>();
            }
        }

        /// <summary>
        /// Получает диалог между двумя пользователями из их журналов
        /// Объединяет сообщения и сортирует по времени
        /// </summary>
        public static async Task<List<MessageJournalEntry>> GetDialogBetweenUsers(string user1, string user2)
        {
            try
            {
                var user1Journal = await GetUserJournal(user1);
                var user2Journal = await GetUserJournal(user2);

                // Фильтруем сообщения только между этими двумя пользователями
                var user1Messages = user1Journal.Where(m => 
                    (m.Sender == user1 && m.Recipient == user2) || 
                    (m.Sender == user2 && m.Recipient == user1)).ToList();
                
                var user2Messages = user2Journal.Where(m => 
                    (m.Sender == user1 && m.Recipient == user2) || 
                    (m.Sender == user2 && m.Recipient == user1)).ToList();

                // Объединяем и сортируем по времени
                var allMessages = user1Messages.Concat(user2Messages)
                    .OrderBy(m => DateTime.Parse(m.Timestamp))
                    .ToList();

                return allMessages;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Ошибка при получении диалога {user1} <-> {user2}: {ex.Message}");
                return new List<MessageJournalEntry>();
            }
        }

        /// <summary>
        /// Очищает журнал пользователя
        /// </summary>
        public static async Task ClearUserJournal(string username)
        {
            try
            {
                string journalFile = Path.Combine(JournalDirectory, $"{username}_journal.json");
                
                if (File.Exists(journalFile))
                {
                    await File.WriteAllTextAsync(journalFile, "[]");
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Журнал пользователя {username} очищен");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Ошибка при очистке журнала {username}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Запись в журнале сообщений
    /// </summary>
    public class MessageJournalEntry
    {
        public string Timestamp { get; set; } = ""; // Точное время отправки
        public string Sender { get; set; } = "";
        public string Recipient { get; set; } = "";
        public string Text { get; set; } = "";
        public string MessageType { get; set; } = "Text";
        public string TimeDisplay { get; set; } = ""; // Время для отображения (HH:mm)
        public string MessageString { get; set; } = ""; // Полная строка сообщения
        public int SequenceNumber { get; set; } = 0; // Порядковый номер в журнале пользователя
    }
}
