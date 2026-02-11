using System.Net.WebSockets;

namespace TebegramServer.Classes.VoiceClasses
{
    public class VoiceRoom
    {
        private int _Id;
        private List<RoomMember> _RoomMembers;
        private string _RoomToken;

        public int Id { get { return _Id; } }
        public List<RoomMember> RoomMembers { get { return _RoomMembers; } }
        public string RoomToken { get { return _RoomToken; } }

        public VoiceRoom(int id, string roomToken)
        {
            _Id = id;
            _RoomToken = roomToken;
            _RoomMembers = new List<RoomMember>();
        }

        public void AddMember(RoomMember roomMember)
        {
            _RoomMembers.Add(roomMember);
        }
        public async Task RemoveMember(WebSocket roomMember, WebSocketCloseStatus webSocketCloseStatus, string desciption, CancellationToken cancellationToken)
        {
            for (int i = 0; i < RoomMembers.Count;i++)
            {
                if (RoomMembers[i].Member == roomMember)
                {
                    await RoomMembers[i].Disconnect(webSocketCloseStatus,desciption,cancellationToken);

                    _RoomMembers.Remove(RoomMembers[i]);
                }
            }
        }

        public void SendVoiceToRoom(WebSocket member, byte[] voice)
        {
            foreach (RoomMember roomMember in _RoomMembers)
            {
                if (roomMember.Member == member)
                {
                    continue;
                }
                else
                {
                    roomMember.SendMe(voice);
                }
            }
        }
    }
}
