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

        public static string CreateRoom(string UsernameCreator)
        {
            string roomToken = new TokenGenerator().GetToken(UsernameCreator);
            VoiceRoom VR = new VoiceRoom(VoiceRooms.Count, roomToken);
            VoiceRooms.Add(roomToken, VR);
            VoiceRoomTokens.Add(roomToken);
            Console.WriteLine($"Создана новая комната:\nId: {VR.Id}\nToken: {VR.RoomToken}");

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
            if (VoiceRooms[Token].RoomMembers.Count < 1)
            {
                int voiceId = VoiceRooms[Token].Id;
                VoiceRooms.Remove(Token);
                VoiceRoomTokens.Remove(Token);
                Console.WriteLine($"Комната Id: {voiceId} удалена иза отсутсвующих учасников");
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
                for (int i = VoiceRooms.Count - 1; i >= 0; i--)
                {
                    if (((VoiceRooms[VoiceRoomTokens[i]].CreatedTime - DateTime.Now).Minutes > 5)
                        && ((VoiceRooms[VoiceRoomTokens[i]].LastDiscconectTime - DateTime.Now).Minutes > 5))
                    {
                        VoiceRooms.Remove(VoiceRoomTokens[i]);
                        VoiceRoomTokens.Remove(VoiceRoomTokens[i]);
                    }
                }

                Thread.Sleep(30000);
            }
        }
    }
}
