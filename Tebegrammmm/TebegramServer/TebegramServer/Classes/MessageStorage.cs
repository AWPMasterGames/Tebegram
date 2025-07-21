using System.Text.Json;

namespace TebegramServer.Classes
{
    public static class MessageStorage
    {
        private static readonly string MessagesDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Messages");

        static MessageStorage()
        {
            if (!Directory.Exists(MessagesDirectory))
            {
                Directory.CreateDirectory(MessagesDirectory);
            }
        }

        public static async Task SaveMessage(string fromUser, string toUser, string message, string timestamp, string messageType, string status = "Sent")
        {
            var messageData = new
            {
                FromUser = fromUser,
                ToUser = toUser,
                Message = message,
                Timestamp = timestamp,
                MessageType = messageType,
                Status = status,
                SavedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            string json = JsonSerializer.Serialize(messageData);
            
            // Сохраняем для отправителя
            await SaveMessageForUser(fromUser, toUser, json);
            
            // Сохраняем для получателя
            await SaveMessageForUser(toUser, fromUser, json);
        }

        private static async Task SaveMessageForUser(string user, string chatWith, string messageJson)
        {
            string userFile = Path.Combine(MessagesDirectory, $"{user}.json");
            List<object> userMessages = new List<object>();

            if (File.Exists(userFile))
            {
                string existingContent = await File.ReadAllTextAsync(userFile);
                if (!string.IsNullOrEmpty(existingContent))
                {
                    try
                    {
                        var existing = JsonSerializer.Deserialize<List<object>>(existingContent);
                        if (existing != null)
                        {
                            userMessages = existing;
                        }
                    }
                    catch
                    {
                        // Если файл поврежден, создаем новый список
                        userMessages = new List<object>();
                    }
                }
            }

            userMessages.Add(JsonSerializer.Deserialize<object>(messageJson));
            
            string updatedJson = JsonSerializer.Serialize(userMessages, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            
            await File.WriteAllTextAsync(userFile, updatedJson);
        }

        public static async Task<List<object>> GetUserMessages(string userLogin)
        {
            string userFile = Path.Combine(MessagesDirectory, $"{userLogin}.json");
            
            if (!File.Exists(userFile))
            {
                return new List<object>();
            }

            string content = await File.ReadAllTextAsync(userFile);
            if (string.IsNullOrEmpty(content))
            {
                return new List<object>();
            }

            try
            {
                var messages = JsonSerializer.Deserialize<List<object>>(content);
                return messages ?? new List<object>();
            }
            catch
            {
                return new List<object>();
            }
        }

        public static async Task ClearUserMessages(string userLogin)
        {
            string userFile = Path.Combine(MessagesDirectory, $"{userLogin}.json");
            
            if (File.Exists(userFile))
            {
                await File.WriteAllTextAsync(userFile, "[]");
            }
        }

        public static async Task UpdateMessageStatus(string fromUser, string toUser, string message, string timestamp, string newStatus)
        {
            try
            {
                // Обновляем для отправителя
                await UpdateMessageStatusForUser(fromUser, toUser, message, timestamp, newStatus);
                
                // Обновляем для получателя
                await UpdateMessageStatusForUser(toUser, fromUser, message, timestamp, newStatus);
                
                Logs.Save($"[MessageStorage] Updated message status: {fromUser} -> {toUser}, status: {newStatus}");
            }
            catch (Exception ex)
            {
                Logs.Save($"[MessageStorage] Error updating message status: {ex.Message}");
                throw;
            }
        }

        private static async Task UpdateMessageStatusForUser(string user, string chatWith, string message, string timestamp, string newStatus)
        {
            try
            {
                string userFile = Path.Combine(MessagesDirectory, $"{user}.json");
                
                if (!File.Exists(userFile))
                {
                    return; // Файл не существует
                }

                string existingContent = await File.ReadAllTextAsync(userFile);
                var userMessages = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(existingContent) ?? new List<Dictionary<string, object>>();

                // Ищем сообщение для обновления
                bool updated = false;
                foreach (var msg in userMessages)
                {
                    if (msg.ContainsKey("Message") && msg.ContainsKey("Timestamp") &&
                        msg["Message"]?.ToString() == message && 
                        msg["Timestamp"]?.ToString() == timestamp)
                    {
                        msg["Status"] = newStatus;
                        updated = true;
                        break;
                    }
                }

                if (updated)
                {
                    string updatedJson = JsonSerializer.Serialize(userMessages, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(userFile, updatedJson);
                    Logs.Save($"[MessageStorage] Updated status for user {user}");
                }
            }
            catch (Exception ex)
            {
                Logs.Save($"[MessageStorage] Error updating status for user {user}: {ex.Message}");
                throw;
            }
        }
        
        public static async Task MarkMessagesAsDelivered(string fromUser, string toUser)
        {
            try
            {
                // Обновляем статус сообщений у отправителя - с Pending на Sent
                await UpdateMessagesStatus(fromUser, toUser, "Pending", "Sent");
                
                Logs.Save($"[MessageStorage] Сообщения от {fromUser} к {toUser} помечены как доставленные");
            }
            catch (Exception ex)
            {
                Logs.Save($"[MessageStorage] Error marking messages as delivered from {fromUser} to {toUser}: {ex.Message}");
                throw;
            }
        }
        
        private static async Task UpdateMessagesStatus(string user, string chatWith, string oldStatus, string newStatus)
        {
            try
            {
                string userFile = Path.Combine(MessagesDirectory, $"{user}.json");
                
                if (!File.Exists(userFile))
                {
                    return; // Нет файла - нет сообщений
                }

                string existingContent = await File.ReadAllTextAsync(userFile);
                if (string.IsNullOrEmpty(existingContent))
                {
                    return;
                }

                var userMessages = JsonSerializer.Deserialize<List<JsonElement>>(existingContent) ?? new List<JsonElement>();
                bool hasChanges = false;

                for (int i = 0; i < userMessages.Count; i++)
                {
                    var messageElement = userMessages[i];
                    
                    if (messageElement.TryGetProperty("ToUser", out var toUserProp) &&
                        messageElement.TryGetProperty("Status", out var statusProp) &&
                        toUserProp.GetString() == chatWith &&
                        statusProp.GetString() == oldStatus)
                    {
                        // Создаем обновленное сообщение
                        var messageDict = new Dictionary<string, object>();
                        
                        foreach (var prop in messageElement.EnumerateObject())
                        {
                            if (prop.Name == "Status")
                            {
                                messageDict[prop.Name] = newStatus;
                            }
                            else
                            {
                                messageDict[prop.Name] = prop.Value.GetRawText().Trim('"');
                            }
                        }
                        
                        userMessages[i] = JsonSerializer.SerializeToElement(messageDict);
                        hasChanges = true;
                    }
                }

                if (hasChanges)
                {
                    string updatedJson = JsonSerializer.Serialize(userMessages, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(userFile, updatedJson);
                    Logs.Save($"[MessageStorage] Updated messages status from {oldStatus} to {newStatus} for user {user} chatting with {chatWith}");
                }
            }
            catch (Exception ex)
            {
                Logs.Save($"[MessageStorage] Error updating messages status for user {user}: {ex.Message}");
                throw;
            }
        }
    }
}
