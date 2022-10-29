using GSPlatformBackServer.Helpers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace GSPlatformBackServer.RoomServer
{
    public class MainRoomServer : UdpServer<MainRoomServer.Message>
    {
        private static readonly TimeSpan RemoveDeadUserInterval = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan RemoveDeadUserTimeOut = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan ResendLaunchEventsInterval = TimeSpan.FromSeconds(0.5);
        private static readonly TimeSpan ResendLaunchEventsRetry = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan ResendLaunchEventsRetryTimeOut = TimeSpan.FromSeconds(10);
        private static uint _seqenceId;

        //TODO use multiple, overlapping structs and send the correct one (see the HandleMessage below)
        public struct Message : IMessageBuffer
        {
            public MessageHeader Header;
            public uint Address;
            public ushort Port;

            public void Init() => Header.Init();
            public bool Check(int size) => Header.Check(size);
        }

        public MainRoomServer() : base(ServerInfo.MainRoomServerPort, 1)
        {
            AddDelayedTask(RemoveDeadUsers);
            AddDelayedTask(ResendLaunchEvents);
            AddDelayedTask(BroadcastRoomEvents);
        }

        private DateTime _removeDeadUserTime = DateTime.MinValue;
        private void RemoveDeadUsers(int threadId, Socket socket, ref Message buffer)
        {
            if (threadId != 0 || _removeDeadUserTime > DateTime.Now) return;
            _removeDeadUserTime = DateTime.Now + RemoveDeadUserInterval;
            RoomServerState.RemoveDeadUsers(DateTime.Now - RemoveDeadUserTimeOut);
        }

        private sealed class PendingLaunchAckInfo
        {
            public ulong RoomUserToken;
            public IPEndPoint Peer = null!;
            public IPEndPoint HostPeer = null!;
            public DateTime NextSend;
            public DateTime TimeOut;
        }
        private readonly ConcurrentQueue<PendingLaunchAckInfo> _pendingLaunchAckSendQueue = new();
        private readonly ConcurrentDictionary<ulong, IPEndPoint> _pendingLaunchAck = new();
        private readonly ConcurrentDictionary<ulong, int> _receivedLaunchAck = new();
        private DateTime _resendLaunchEventsTime = DateTime.MinValue;
        private void ResendLaunchEvents(int threadId, Socket socket, ref Message buffer)
        {
            if (threadId != 0 || _resendLaunchEventsTime > DateTime.Now) return;
            var now = DateTime.Now;
            _resendLaunchEventsTime = now + ResendLaunchEventsInterval;

            //We need to put back some tasks, so there needs to be a limit of tasks to process.
            int count = _pendingLaunchAckSendQueue.Count;

            //TODO log if the count is large

            while (--count >= 0 && _pendingLaunchAckSendQueue.TryDequeue(out var task))
            {
                if (_receivedLaunchAck.TryRemove(task.RoomUserToken, out _))
                {
                    //Received.
                    _pendingLaunchAck.TryRemove(task.RoomUserToken, out _);
                    continue;
                }
                if (task.TimeOut < now)
                {
                    //Time out.
                    _pendingLaunchAck.TryRemove(task.RoomUserToken, out _);
                    continue;
                }

                //Note that the room now has been deleted. We should only depend on info in the task.

                if (task.NextSend < now)
                {
                    buffer.Init();
                    buffer.Header.MessageType = MessageType.LaunchEvent;
                    buffer.Header.ClientToken = ulong.MaxValue;
                    buffer.Header.SequenceId = (ushort)Interlocked.Increment(ref _seqenceId);
                    buffer.Address = task.HostPeer.Address.GetIPv4AddressNumber();
                    buffer.Port = (ushort)task.HostPeer.Port;
                    socket.SendTo(ref buffer, task.Peer);
                    task.NextSend += ResendLaunchEventsRetry;
                }
                _pendingLaunchAckSendQueue.Enqueue(task);
            }
        }

        private void BroadcastRoomEvents(int threadId, Socket socket, ref Message buffer)
        {
            //This task is embarrassingly parallel. We can simply do it on any thread.
            while (RoomServerState.GetRoomEventBroadcastTask(out var peer))
            {
                buffer.Init();
                buffer.Header.MessageType = MessageType.RoomEvent;
                buffer.Header.ClientToken = ulong.MaxValue; //Broadcast event cannot have client token.
                buffer.Header.SequenceId = (ushort)Interlocked.Increment(ref _seqenceId);
                socket.SendTo(ref buffer.Header, peer); //No payload. Only send header.
            }
        }

        protected override void HandleMessage(Socket socket, IPEndPoint src, ref Message msg)
        {
            if (_pendingLaunchAck.TryGetValue(msg.Header.ClientToken, out var ep) &&
                ep.MappedEquals(src))
            {
                _receivedLaunchAck.TryAdd(msg.Header.ClientToken, 0);
            }

            var room = RoomServerState.FindRoomByRoomUserToken(msg.Header.ClientToken);
            if (room is null)
            {
                LogHelpers.WriteLog(src, "Room not found", null);
                return;
            }
            if (!room.EnsureUserJoined(msg.Header.ClientToken, src))
            {
                return;
            }

            switch (msg.Header.MessageType)
            {
            case MessageType.Self:
                room.PingReceived(msg.Header.ClientToken);
                msg.Init();
                msg.Header.MessageType = MessageType.SelfReply;
                msg.Address = src.Address.GetIPv4AddressNumber();
                msg.Port = (ushort)(uint)src.Port;
                socket.SendTo(ref msg, src);
                break;
            case MessageType.Launch:
                //Check host.
                var host = room.OwnerEndPoint;
                if (!src.MappedEquals(host))
                {
                    //TODO log
                    break;
                }

                //Send launch ack.
                //Client should repeat Launch event until it gets this ack (or a LaunchEvent if the ack is lost).
                msg.Init();
                msg.Header.MessageType = MessageType.Ack;
                socket.SendTo(ref msg.Header, src); //No payload.

                //Add pending launch event ack to list.
                room.EnumerateMembers(static (token, ep, args) =>
                {
                    var (now, server, queue, list) = args;
                    list.TryAdd(token, ep);
                    queue.Enqueue(new PendingLaunchAckInfo()
                    {
                        RoomUserToken = token,
                        Peer = ep,
                        HostPeer = server,
                        NextSend = now + ResendLaunchEventsRetry,
                        TimeOut = now + ResendLaunchEventsRetryTimeOut,
                    });
                }, (DateTime.Now, host, _pendingLaunchAckSendQueue, _pendingLaunchAck));

                //Broadcast launch event.
                msg.Init();
                msg.Header.MessageType = MessageType.LaunchEvent;
                msg.Header.ClientToken = ulong.MaxValue; //Broadcast event cannot have client token.
                msg.Header.SequenceId = (ushort)Interlocked.Increment(ref _seqenceId);
                room.SendHostEndPointToAllMembers(socket, ref msg); //This method will correct the host endpoint.

                //Switch room state. This should remove room from list.
                room.Launch();

                break;
            case MessageType.LaunchEventAck:
                //Invalid ack.
                //Valid ack has been handled above.
                break;
            default:
                break;
            }
        }
    }
}
