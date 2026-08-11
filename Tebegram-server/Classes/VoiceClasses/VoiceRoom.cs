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

        private DateTime _CreatedTime { get; set; }
        public DateTime CreatedTime { get { return _CreatedTime; } }
        private DateTime _LastDiscconectTime { get; set; }
        public DateTime LastDiscconectTime { get { return _LastDiscconectTime; } }
        public VoiceRoom(int id, string roomToken)
        {
            _Id = id;
            _RoomToken = roomToken;
            _RoomMembers = new List<RoomMember>();
            _CreatedTime = DateTime.Now;
        }

        public void AddMember(RoomMember roomMember)
        {
            _RoomMembers.Add(roomMember);
        }
        public async Task RemoveMember(WebSocket roomMember, WebSocketCloseStatus webSocketCloseStatus, string? desciption, CancellationToken cancellationToken)
        {
            for (int i = 0; i < RoomMembers.Count; i++)
            {
                if (RoomMembers[i].Member == roomMember)
                {
                    await RoomMembers[i].Disconnect(webSocketCloseStatus, desciption, cancellationToken);

                    _RoomMembers.Remove(RoomMembers[i]);
                    _LastDiscconectTime = DateTime.Now;
                    break;
                }
            }
        }

        public async Task SendVoiceToRoom(WebSocket member, byte[] voice)
        {
            // Снимок списка: участники могут отключаться во время рассылки.
            // await обязателен - два параллельных SendAsync на одном сокете кидают исключение.
            foreach (RoomMember roomMember in _RoomMembers.ToList())
            {
                if (roomMember.Member == member)
                {
                    continue;
                }
                await roomMember.SendMe(voice);
            }
        }

        public async Task SendTextToRoom(string text)
        {
            foreach (RoomMember roomMember in _RoomMembers.ToList())
            {
                await roomMember.SendMeText(text);
            }
        }

        /// <summary>
        /// Текст всем, КРОМЕ отправителя. Нужно для состояния микрофона: значок
        /// рядом с аватаром показывает микрофон СОБЕСЕДНИКА, поэтому своё же
        /// уведомление возвращать себе нельзя - иначе оно перебьёт свой значок.
        /// </summary>
        public async Task SendTextToRoomExcept(WebSocket sender, string text)
        {
            // Снимок списка: участники могут отключаться во время рассылки
            foreach (RoomMember roomMember in _RoomMembers.ToList())
            {
                if (roomMember.Member == sender) continue;
                await roomMember.SendMeText(text);
            }
        }
    }
}
