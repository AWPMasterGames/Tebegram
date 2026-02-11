using System.Net.WebSockets;
using System.Threading.Tasks;
using TebegramServer.Classes.VoiceClasses;
using TebegramServer.Tools;

namespace TebegramServer.Controllers
{
    public static class VoiceRoomsController
    {
        //private static List<VoiceRoom> _VoiceRooms1 = new List<VoiceRoom>();
        public static Dictionary<string, VoiceRoom> VoiceRooms = new Dictionary<string, VoiceRoom>();

        public static string CreateRoom(string UsernameCreator)
        {
            string roomToken = new TokenGenerator().GetToken(UsernameCreator);
            VoiceRoom VR = new VoiceRoom(VoiceRooms.Count, roomToken);
            VoiceRooms.Add(roomToken, VR);

            Console.WriteLine($"Создана новая комната:\nId: {VR.Id}\nToken: {VR.RoomToken}");

            return roomToken;
        }

        public static void ConnectingToRoom(WebSocket webSocket, string Token, User user)
        {
            VoiceRooms[Token].AddMember(new RoomMember(user, webSocket));
        }

        public static async Task DisconnectFromRoom(WebSocket webSocket,string Token, WebSocketCloseStatus webSocketCloseStatus, string desciption, CancellationToken cancellationToken)
        {
            await VoiceRooms[Token].RemoveMember(webSocket, webSocketCloseStatus,desciption,cancellationToken);
            if (VoiceRooms[Token].RoomMembers.Count < 1)
            {
                Console.WriteLine($"Комната Id: {VoiceRooms[Token].Id} удалена иза отсутсвующих учасников");
                VoiceRooms.Remove(Token);
            }
        }

        public static int GetRoomId(string Token)
        {
            return VoiceRooms[Token].Id;
        }
    }
}
