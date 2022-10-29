using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace GSPlatformBackServer.RoomServer
{
    public enum MessageType : byte
    {
        Ack = 0x10,

        //S->C. () -> No reply.
        RoomEvent = 0x11,

        //C->S. () -> Ack. Owner only.
        Launch = 0x12,
        //S->C. () -> LaunchEventAck().
        LaunchEvent = 0x13,
        LaunchEventAck = 0x14, //TODO merge with Ack

        //C->S. () -> SelfReply(EndPoint).
        Self = 0x15,
        SelfReply = 0x16,

        //C->C. () -> Ack.
        //For C->S ping, use Self->SelfReply.
        Ping = 0x17,
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct MessageHeader
    {
        [FieldOffset(0)]
        public byte AMLMessageId;
        [FieldOffset(1)]
        public uint Magic;
        [FieldOffset(5)]
        public MessageType MessageType;
        [FieldOffset(6)]
        public ushort SequenceId;
        [FieldOffset(8)]
        public ulong ClientToken;

        public const uint MagicValue = 0x46505347u;

        public void Init()
        {
            AMLMessageId = 0xF0;
            Magic = MagicValue;
        }

        public bool Check(int length)
        {
            return length >= Unsafe.SizeOf<MessageHeader>() && AMLMessageId == 0xF0 && Magic == MagicValue;
        }
    }
}
