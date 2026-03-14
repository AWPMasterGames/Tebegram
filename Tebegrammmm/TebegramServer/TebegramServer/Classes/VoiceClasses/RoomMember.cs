using System.Net.WebSockets;
using System.Text;

namespace TebegramServer.Classes.VoiceClasses
{
    public class RoomMember
    {
        private User _User;
        private WebSocket _Member;
        public WebSocket Member { get { return _Member; } }
        public User User { get { return _User; } }

        public RoomMember(User user, WebSocket member)
        {
            _User = user;
            _Member = member;
        }

        public async Task SendMe(byte[] message)
        {
            var bytes = message;

            if (_Member.State == WebSocketState.Open)
            {
                var arraySegment = new ArraySegment<byte>(bytes, 0, bytes.Length);
                await _Member.SendAsync(arraySegment, WebSocketMessageType.Binary, true, CancellationToken.None);
            }
        }

        public async Task Disconnect(WebSocketCloseStatus webSocketCloseStatus, string desciption,CancellationToken cancellationToken)
        {
            await Member.CloseAsync(webSocketCloseStatus, desciption, cancellationToken);
            User.CallToken = "";
        }
    }
}
