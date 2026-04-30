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
        private static readonly object _roomsLock = new object();

        public static string CreateRoom(string UsernameCreator)
        {
            string roomToken = new TokenGenerator().GetToken(UsernameCreator);
            lock (_roomsLock)
            {
                VoiceRoom VR = new VoiceRoom(VoiceRooms.Count, roomToken);
                VoiceRooms.Add(roomToken, VR);
                VoiceRoomTokens.Add(roomToken);
                Console.WriteLine($"Создана новая комната:\nId: {VR.Id}\nToken: {VR.RoomToken}");
            }
            return roomToken;
        }

        public static async Task RemoveRoom(string token)
        {
            
        }

        public static void ConnectingToRoom(WebSocket webSocket, string Token, User user)
        {
            VoiceRooms[Token].AddMember(new RoomMember(user, webSocket));
        }

        public static async Task DisconnectFromRoom(WebSocket webSocket, string Token, WebSocketCloseStatus webSocketCloseStatus, string desciption, CancellationToken cancellationToken)
        {
            await VoiceRooms[Token].RemoveMember(webSocket, webSocketCloseStatus, desciption, cancellationToken);
            lock (_roomsLock)
            {
                if (VoiceRooms.TryGetValue(Token, out var room) && room.RoomMembers.Count < 1)
                {
                    int voiceId = room.Id;
                    VoiceRooms.Remove(Token);
                    VoiceRoomTokens.Remove(Token);
                    Console.WriteLine($"Комната Id: {voiceId} удалена из-за отсутствующих участников");
                }
            }
        }

        public static int GetRoomId(string Token)
        {
            try
            {
                return VoiceRooms[Token].Id;
            }
            catch
            {
                return -1;
            }
        }

        public static async void CheckEmptyVoices()
        {
            while (true)
            {
                lock (_roomsLock)
                {
                    for (int i = VoiceRoomTokens.Count - 1; i >= 0; i--)
                    {
                        var room = VoiceRooms[VoiceRoomTokens[i]];
                        bool isEmpty = room.RoomMembers.Count == 0;
                        bool createdLongAgo = (DateTime.Now - room.CreatedTime).TotalMinutes > 5;
                        bool disconnectedLongAgo = room.LastDisconnectTime != default &&
                                                   (DateTime.Now - room.LastDisconnectTime).TotalMinutes > 5;
                        if (isEmpty && (disconnectedLongAgo || createdLongAgo))
                        {
                            Console.WriteLine($"Комната Id: {room.Id} удалена по таймауту");
                            VoiceRooms.Remove(VoiceRoomTokens[i]);
                            VoiceRoomTokens.RemoveAt(i);
                        }
                    }
                }

                Thread.Sleep(30000);
            }
        }
    }
}
