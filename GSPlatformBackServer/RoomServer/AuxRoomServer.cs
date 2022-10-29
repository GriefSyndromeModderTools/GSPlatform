using GSPlatformBackServer.Helpers;
using System.Net;
using System.Net.Sockets;

namespace GSPlatformBackServer.RoomServer
{
    public class AuxRoomServer : UdpServer<AuxRoomServer.Message>
    {
        public struct Message : IMessageBuffer
        {
            public MessageHeader Header;
            public uint Address;
            public ushort Port;

            public void Init() => Header.Init();
            public bool Check(int size) => Header.Check(size);
        }

        public AuxRoomServer() : base(ServerInfo.AuxRoomServerPort, 1)
        {
        }

        protected override void HandleMessage(Socket socket, IPEndPoint src, ref Message msg)
        {
            var room = RoomServerState.FindRoomByRoomUserToken(msg.Header.ClientToken);
            if (room is null)
            {
                LogHelpers.WriteLog(src, "Room not found", null);
                return;
            }
            room.EnsureUserJoined(msg.Header.ClientToken, src);

            switch (msg.Header.MessageType)
            {
            case MessageType.Self:
                msg.Init();
                msg.Header.MessageType = MessageType.SelfReply;
                msg.Address = src.Address.GetIPv4AddressNumber();
                msg.Port = (ushort)(uint)src.Port;
                socket.SendTo(ref msg, src);
                break;
            default:
                break;
            }
        }
    }
}
