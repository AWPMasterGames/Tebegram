using System.Net.WebSockets;
using System.Threading.Tasks;
using TebegramServer.Classes.VoiceClasses;
using TebegramServer.Tools;

namespace TebegramServer.Controllers
{
    public static class VoiceRoomsController
    {
        public static Dictionary<string, VoiceRoom> VoiceRooms = new Dictionary<string, VoiceRoom>();
        private static List<string> VoiceRoomTokens = new List<string>();
        private static readonly object _lock = new object();
        private static int _nextRoomId = 0;

        public static string CreateRoom(string UsernameCreator)
        {
            lock (_lock)
            {
                string roomToken = new TokenGenerator().GetToken(UsernameCreator);
                // Счётчик вместо VoiceRooms.Count - Id комнат не должны повторяться после удаления комнат
                VoiceRoom VR = new VoiceRoom(_nextRoomId++, roomToken);
                VoiceRooms.Add(roomToken, VR);
                VoiceRoomTokens.Add(roomToken);
                Console.WriteLine($"Создана новая комната:\nId: {VR.Id}\nToken: {VR.RoomToken}");

                return roomToken;
            }
        }

        public static void ConnectingToRoom(WebSocket webSocket, string Token, User user)
        {
            lock (_lock)
            {
                if (VoiceRooms.TryGetValue(Token, out var room))
                {
                    room.AddMember(new RoomMember(user, webSocket));
                }
            }
        }

        public static async Task DisconnectFromRoom(WebSocket webSocket, string Token, WebSocketCloseStatus webSocketCloseStatus, string? desciption, CancellationToken cancellationToken)
        {
            VoiceRoom? room;
            lock (_lock)
            {
                // Комната могла быть уже удалена (двойное отключение) - раньше падал KeyNotFoundException
                if (!VoiceRooms.TryGetValue(Token, out room)) return;
            }

            await room.RemoveMember(webSocket, webSocketCloseStatus, desciption, cancellationToken);

            lock (_lock)
            {
                if (VoiceRooms.TryGetValue(Token, out room) && room.RoomMembers.Count < 1)
                {
                    int voiceId = room.Id;
                    VoiceRooms.Remove(Token);
                    VoiceRoomTokens.Remove(Token);
                    Console.WriteLine($"Комната Id: {voiceId} удалена иза отсутсвующих учасников");
                }
            }
        }

        public static int GetRoomId(string Token)
        {
            lock (_lock)
            {
                return VoiceRooms.TryGetValue(Token, out var room) ? room.Id : -1;
            }
        }

        public static void CheckEmptyVoices()
        {
            while (true)
            {
                lock (_lock)
                {
                    for (int i = VoiceRoomTokens.Count - 1; i >= 0; i--)
                    {
                        string token = VoiceRoomTokens[i];
                        if (!VoiceRooms.TryGetValue(token, out var room)) { VoiceRoomTokens.RemoveAt(i); continue; }

                        // Раньше считалось (CreatedTime - Now) - всегда отрицательное, комнаты не чистились никогда
                        bool isOldEnough = (DateTime.Now - room.CreatedTime).TotalMinutes > 5;
                        bool isAbandoned = room.RoomMembers.Count == 0
                            || (room.LastDiscconectTime != default && (DateTime.Now - room.LastDiscconectTime).TotalMinutes > 5 && room.RoomMembers.Count == 0);

                        if (isOldEnough && isAbandoned)
                        {
                            VoiceRooms.Remove(token);
                            VoiceRoomTokens.RemoveAt(i);
                            Console.WriteLine($"Комната Id: {room.Id} удалена чисткой пустых комнат");
                        }
                    }
                }

                Thread.Sleep(30000);
            }
        }
    }
}
