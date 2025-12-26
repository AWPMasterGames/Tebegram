#nullable disable
using System.Text.Json;
using TebegramServer.Classes;

namespace TebegramServer.Data
{
    public static class UserActivityStorage
    {
        private static readonly string StorageDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserActivity");

        static UserActivityStorage()
        {
            Directory.CreateDirectory(StorageDirectory);
        }

        public static async Task UpdateActivity(string userId, string lastMessageLoad)
        {
            try
            {
                var activity = new Dictionary<string, object>
                {
                    ["userId"] = userId,
                    ["lastMessageLoad"] = lastMessageLoad,
                    ["updatedAt"] = DateTime.Now.ToString("o")
                };

                string filePath = Path.Combine(StorageDirectory, $"{userId}_activity.json");
                string json = JsonSerializer.Serialize(activity, new JsonSerializerOptions { WriteIndented = true });
                
                await File.WriteAllTextAsync(filePath, json);
                Logs.Save($"[UserActivityStorage] Updated activity for user {userId}");
            }
            catch (Exception ex)
            {
                Logs.Save($"[UserActivityStorage] Error updating activity for {userId}: {ex.Message}");
                throw;
            }
        }

        public static async Task<Dictionary<string, object>> GetUserActivity(string userId)
        {
            try
            {
                string filePath = Path.Combine(StorageDirectory, $"{userId}_activity.json");
                
                if (!File.Exists(filePath))
                {
                    // Возвращаем активность по умолчанию, если файла нет
                    return new Dictionary<string, object>
                    {
                        ["userId"] = userId,
                        ["lastMessageLoad"] = DateTime.MinValue.ToString("o"),
                        ["updatedAt"] = DateTime.MinValue.ToString("o")
                    };
                }

                string json = await File.ReadAllTextAsync(filePath);
                var activity = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                
                Logs.Save($"[UserActivityStorage] Retrieved activity for user {userId}");
                return activity ?? new Dictionary<string, object>();
            }
            catch (Exception ex)
            {
                Logs.Save($"[UserActivityStorage] Error getting activity for {userId}: {ex.Message}");
                
                // Возвращаем активность по умолчанию в случае ошибки
                return new Dictionary<string, object>
                {
                    ["userId"] = userId,
                    ["lastMessageLoad"] = DateTime.MinValue.ToString("o"),
                    ["updatedAt"] = DateTime.MinValue.ToString("o")
                };
            }
        }

        public static Task ClearUserActivity(string userId)
        {
            try
            {
                string filePath = Path.Combine(StorageDirectory, $"{userId}_activity.json");
                
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Logs.Save($"[UserActivityStorage] Cleared activity for user {userId}");
                }
                
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Logs.Save($"[UserActivityStorage] Error clearing activity for {userId}: {ex.Message}");
                throw;
            }
        }

        public static async Task SetOpenChat(string userId, string chatWithUser)
        {
            try
            {
                var activity = await GetUserActivity(userId);
                activity["openChatWith"] = chatWithUser;
                activity["openChatAt"] = DateTime.Now.ToString("o");
                
                string filePath = Path.Combine(StorageDirectory, $"{userId}_activity.json");
                string json = JsonSerializer.Serialize(activity, new JsonSerializerOptions { WriteIndented = true });
                
                await File.WriteAllTextAsync(filePath, json);
                Logs.Save($"[UserActivityStorage] User {userId} opened chat with {chatWithUser}");
            }
            catch (Exception ex)
            {
                Logs.Save($"[UserActivityStorage] Error setting open chat for {userId}: {ex.Message}");
                throw;
            }
        }

        public static async Task ClearOpenChat(string userId)
        {
            try
            {
                var activity = await GetUserActivity(userId);
                activity.Remove("openChatWith");
                activity.Remove("openChatAt");
                
                string filePath = Path.Combine(StorageDirectory, $"{userId}_activity.json");
                string json = JsonSerializer.Serialize(activity, new JsonSerializerOptions { WriteIndented = true });
                
                await File.WriteAllTextAsync(filePath, json);
                Logs.Save($"[UserActivityStorage] User {userId} closed chat");
            }
            catch (Exception ex)
            {
                Logs.Save($"[UserActivityStorage] Error clearing open chat for {userId}: {ex.Message}");
                throw;
            }
        }

        public static async Task<Dictionary<string, object>> GetOpenChat(string userId)
        {
            try
            {
                var activity = await GetUserActivity(userId);
                
                var openChat = new Dictionary<string, object>
                {
                    ["userId"] = userId,
                    ["openChatWith"] = activity.ContainsKey("openChatWith") ? activity["openChatWith"] : "",
                    ["openChatAt"] = activity.ContainsKey("openChatAt") ? activity["openChatAt"] : DateTime.MinValue.ToString("o")
                };
                
                Logs.Save($"[UserActivityStorage] Retrieved open chat for user {userId}");
                return openChat;
            }
            catch (Exception ex)
            {
                Logs.Save($"[UserActivityStorage] Error getting open chat for {userId}: {ex.Message}");
                
                return new Dictionary<string, object>
                {
                    ["userId"] = userId,
                    ["openChatWith"] = "",
                    ["openChatAt"] = DateTime.MinValue.ToString("o")
                };
            }
        }
    }
}
