using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GSPlatformClient
{
    internal struct ServerPeerState
    {
        public string UserName;
        public bool CanRelay;
        public bool IsRelay;
    }

    internal sealed class RoomClient : UdpClient<RoomClient.Message>
    {
        private static readonly TimeSpan PingBatchInterval = TimeSpan.FromSeconds(0.3);
        private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan PingRepeatInterval = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan PingTotalTimeOut = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan ServerPingInterval = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan ServerTimeOutTime = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan LaunchTimeOutTime = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan LaunchRequestRepeat = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan UpdateRoomPeersInterval = TimeSpan.FromSeconds(5);

        public struct Message : IMessageBuffer
        {
            public MessageHeader Header;
            public uint Address;
            public ushort Port;

            public void Init() => Header.Init();
            public bool Check(int size) => Header.Check(size);
        }

        public sealed class Peer
        {
            public readonly IPEndPoint EndPoint;
            public readonly ulong SelfToken;
            public string UserName;

            private DateTime _lastPingSend;
            private ushort _lastPingSeq;
            private int _lastPingRepeat;
            private bool _lastPingReceived;

            private long _totalPingSent;
            private long _totalPingReceived;
            private long _totalPongReceived;

            private int _recentPingReceivedCount;
            private int _recentPingLostCount;
            private float _recentPingDelayMs;
            private const float _pingDelayDecay = 0.8f;

            public float AverageDelayMs => _recentPingDelayMs;
            public float Connectivity => _recentPingReceivedCount + _recentPingLostCount < 2 ? 0f :
                (float)_recentPingReceivedCount / (_recentPingReceivedCount + _recentPingLostCount);

            public bool Connected => _totalPingReceived > 2 && _totalPongReceived > 2;

            //Relay state returned by server API. Only affect UI.
            public bool CanRelay, IsRelay;

            public Peer(IPEndPoint endPoint, ulong selfToken)
            {
                EndPoint = endPoint;
                SelfToken = selfToken;
                _lastPingReceived = true;
                _lastPingRepeat = 0;
                _lastPingSend = DateTime.MinValue;
            }

            public void Ping(Socket s, DateTime now, ref ushort nextSeq, ref Message b, byte[] ba)
            {
                bool send;
                bool isNew;
                if (!_lastPingReceived)
                {
                    isNew = false;

                    //Waiting for last response.
                    send = _lastPingSend + PingRepeatInterval < now;

                    if (now > _lastPingSend + PingTotalTimeOut)
                    {
                        //Give up this sequence id and use a new one.
                        isNew = true;

                        _recentPingLostCount += 1;
                        if (_recentPingLostCount + _recentPingReceivedCount > 200)
                        {
                            _recentPingLostCount <<= 2;
                            _recentPingReceivedCount <<= 1;
                        }
                    }
                }
                else
                {
                    isNew = true;
                    send = _lastPingSend + PingInterval < now;
                }

                if (!send)
                {
                    return;
                }
                _lastPingSend = DateTime.Now;
                _lastPingSeq = isNew ? nextSeq++ : _lastPingSeq;
                _lastPingRepeat = isNew ? 0 : _lastPingRepeat + 1;
                _lastPingReceived = false;
                _totalPingSent += isNew ? 1 : 0;

                b.Header.MessageType = MessageType.Ping;
                b.Header.SequenceId = _lastPingSeq;
                b.Header.ClientToken = SelfToken;
                s.SendTo(ref b, EndPoint, ba);
            }

            public void PingReceived()
            {
                _totalPingReceived += 1;
            }

            public void PingAckReceived(int seq)
            {
                if (!_lastPingReceived && _lastPingSeq == seq)
                {
                    _lastPingReceived = true;
                    _totalPongReceived += 1;

                    _recentPingReceivedCount += 1;
                    if (_recentPingLostCount + _recentPingReceivedCount > 200)
                    {
                        _recentPingLostCount <<= 2;
                        _recentPingReceivedCount <<= 1;
                    }

                    //Only calculate delay if we did not repeat.
                    if (_lastPingRepeat == 0)
                    {
                        var delay = (float)(DateTime.Now - _lastPingSend).TotalMilliseconds;
                        if (_totalPingSent < 2)
                        {
                            _recentPingDelayMs = delay;
                        }
                        else
                        {
                            _recentPingDelayMs = _recentPingDelayMs * _pingDelayDecay + delay * (1 - _pingDelayDecay);
                        }
                    }
                }
            }
        }

        public int HttpServerIndex { get; }
        public string RoomId { get; }
        public ulong RoomToken { get; }
        private readonly IPEndPoint _server;
        private readonly List<Peer> _peers = new List<Peer>(); //Should be locked
        private ushort _nextSeq = 1;

        public bool IsHost { get; set; }
        public IPEndPoint HostEndPoint { get; set; }

        public event Action UpdateRoomPeers;
        public event Action ServerLaunched; //Not invoked when launched by LaunchRequest()
        public event Action ServerTimeOut;

        private bool _launchRequest;
        private bool _launchResult;
        private TaskCompletionSource<int> _launchProcessFinishedEvent;

        private Dictionary<IPEndPoint, ServerPeerState> _updatedPeers;
        private TaskCompletionSource<int> _updatePeersEvent;

        public RoomClient(int httpServerIndex, IPEndPoint server, string roomId, ulong token, bool startAsHost)
        {
            HttpServerIndex = httpServerIndex;
            _server = server;
            RoomId = roomId;
            RoomToken = token;
            IsHost = startAsHost;

            AddDelayedTask(ReceiveUpdatedPeerList);
            AddDelayedTask(SendPing);
            AddDelayedTask(SendServerPing);
            AddDelayedTask(TriggerUpdatePeer);
            AddDelayedTask(LaunchProcess);
        }

        public Peer[] GetPeers()
        {
            lock (_peers)
            {
                return _peers.ToArray();
            }
        }

        public Task UpdatePeers(Dictionary<IPEndPoint, ServerPeerState> peers)
        {
            if (!IsRunning)
            {
                //Removed. This seems only to cause the caller to await forever.
                //Where is Task.CompletedTask in .NET 4.5?
                //return new Task(() => { });
            }
            _updatePeersEvent = new TaskCompletionSource<int>();
            _updatedPeers = peers;
            return Task.WhenAny(_updatePeersEvent.Task, Task.Delay(ServerTimeOutTime));
        }

        private void ReceiveUpdatedPeerList(Socket socket, ref Message buffer, byte[] bufferArray)
        {
            var list = _updatedPeers;
            _updatedPeers = null;
            if (list == null) return;

            lock (_peers)
            {
                for (int i = _peers.Count - 1; i >= 0; --i)
                {
                    var p = _peers[i];
                    if (p.EndPoint.Port == 0) continue;
                    if (list.TryGetValue(p.EndPoint, out var s))
                    {
                        list.Remove(p.EndPoint);
                        p.UserName = s.UserName;
                        p.IsRelay = s.IsRelay;
                        p.CanRelay = s.CanRelay;
                    }
                    else
                    {
                        _peers.RemoveAt(i);
                    }
                }
                foreach (var pair in list)
                {
                    _peers.Add(new Peer(pair.Key, RoomToken)
                    {
                        UserName = pair.Value.UserName,
                        CanRelay = pair.Value.CanRelay,
                        IsRelay = pair.Value.IsRelay,
                    });
                }
            }
            var e = _updatePeersEvent;
            _updatePeersEvent = null;
            e?.SetResult(0);
        }

        public async Task<bool> LaunchRequest()
        {
            var e = new TaskCompletionSource<int>();
            _launchProcessFinishedEvent = e;
            _launchResult = false;
            _launchMessageSent = false;
            _launchEventReceived = false;
            _launchAckReceived = false;
            _lastLaunchMessageSent = DateTime.MinValue;
            _launchRequest = true; //Enter launch process.

            var timeout = Task.Delay(LaunchTimeOutTime);
            if (await Task.WhenAny(e.Task, timeout) != e.Task)
            {
                _launchRequest = false; //Exit launch process.
                ServerTimeOut?.Invoke();
                return false;
            }
            _launchRequest = false; //Exit launch process.
            return _launchResult;
        }

        private DateTime _nextPingBatchTime = DateTime.MinValue;
        private void SendPing(Socket socket, ref Message buffer, byte[] bufferArray)
        {
            var now = DateTime.Now;
            if (now < _nextPingBatchTime)
            {
                return;
            }
            _nextPingBatchTime = now + PingBatchInterval;
            lock (_peers)
            {
                foreach (var p in _peers)
                {
                    p.Ping(socket, now, ref _nextSeq, ref buffer, bufferArray);
                }
            }
        }

        private DateTime _nextServerPingTime = DateTime.MinValue;
        private DateTime? _pendingServerPing = null;
        private void SendServerPing(Socket socket, ref Message buffer, byte[] bufferArray)
        {
            var now = DateTime.Now;
            if (_pendingServerPing + ServerTimeOutTime < now)
            {
                System.Diagnostics.Debug.WriteLine("server timeout");
                _pendingServerPing = null;
                ServerTimeOut?.Invoke();
                return;
            }
            if (now < _nextServerPingTime)
            {
                return;
            }
            _nextServerPingTime = now + ServerPingInterval;

            _pendingServerPing = _pendingServerPing ?? now;
            System.Diagnostics.Debug.WriteLine("server ping: " + _pendingServerPing);
            buffer.Init();
            buffer.Header.MessageType = MessageType.Self;
            buffer.Header.SequenceId = _nextSeq++;
            buffer.Header.ClientToken = RoomToken;
            socket.SendTo(ref buffer.Header, _server, bufferArray);
        }

        private DateTime _nextUpdatePeersTime = DateTime.MinValue;
        private void TriggerUpdatePeer(Socket socket, ref Message buffer, byte[] bufferArray)
        {
            var now = DateTime.Now;
            if (now < _nextUpdatePeersTime)
            {
                return;
            }
            _nextUpdatePeersTime = now + UpdateRoomPeersInterval;

            UpdateRoomPeers?.Invoke();
        }

        private ushort _launchMessageSeq;
        private DateTime _lastLaunchMessageSent;
        private volatile bool _launchMessageSent, _launchEventReceived, _launchAckReceived;
        private void LaunchProcess(Socket socket, ref Message buffer, byte[] bufferArray)
        {
            if (!_launchRequest) return;

            if (!_launchMessageSent)
            {
                //If it's the first time, decide the seq.
                _launchMessageSeq = _nextSeq++;
            }
            if (!_launchAckReceived && _lastLaunchMessageSent + LaunchRequestRepeat <= DateTime.Now)
            {
                //First time, or if the server has not ack'ed.
                _lastLaunchMessageSent = DateTime.Now;
                _launchMessageSent = true;

                buffer.Init();
                buffer.Header.MessageType = MessageType.Launch;
                buffer.Header.SequenceId = _launchMessageSeq;
                buffer.Header.ClientToken = RoomToken;
                socket.SendTo(ref buffer.Header, _server, bufferArray);
            }
            if (_launchEventReceived)
            {
                //Receiving _launchEventReceived is the last step.
                //We can safely start the game now.

                //We don't have a mechanism for the server to refuse the launch now.
                //So when we trigger the event, it always succeeds.
                _launchResult = true;
                _launchProcessFinishedEvent.SetResult(0);
            }
        }

        private bool TryFindPeer(IPEndPoint ep, out Peer peer)
        {
            lock (_peers)
            {
                foreach (var p in _peers)
                {
                    if (p.EndPoint.Equals(ep))
                    {
                        peer = p;
                        return true;
                    }
                }
            }
            peer = null;
            return false;
        }

        protected override void HandleMessage(Socket socket, IPEndPoint src, ref Message msg, byte[] msgArray)
        {
            var isFromServer = src.Equals(_server);
            switch (msg.Header.MessageType)
            {
            case MessageType.Ping:
            {
                if (TryFindPeer(src, out var p))
                {
                    p.PingReceived();
                    msg.Init();
                    msg.Header.MessageType = MessageType.Ack;
                    msg.Header.ClientToken = RoomToken;
                    socket.SendTo(ref msg.Header, src, msgArray);
                }
                break;
            }
            case MessageType.Ack:
            {
                if (src.Equals(_server))
                {
                    if (!_launchAckReceived && _launchMessageSeq == msg.Header.SequenceId)
                    {
                        _launchAckReceived = true;
                    }
                }
                else if (TryFindPeer(src, out var p))
                {
                    p.PingAckReceived(msg.Header.SequenceId);
                }
                break;
            }
            case MessageType.RoomEvent:
                if (isFromServer)
                {
                    _nextUpdatePeersTime = DateTime.Now + UpdateRoomPeersInterval;
                    UpdateRoomPeers?.Invoke();
                }
                break;
            case MessageType.SelfReply:
                if (isFromServer)
                {
                    _pendingServerPing = null;
                    System.Diagnostics.Debug.WriteLine("server ack");
                }
                break;
            case MessageType.LaunchEvent:
                if (isFromServer)
                {
                    HostEndPoint = new IPEndPoint(new IPAddress(msg.Address), msg.Port);

                    //Set _launchEventReceived, which is one step in host's launch process.
                    _launchEventReceived = true;

                    //Reply. Server will continue sending the LaunchEvent if we don't reply.
                    msg.Header.MessageType = MessageType.LaunchEventAck;
                    msg.Header.ClientToken = RoomToken;
                    socket.SendTo(ref msg.Header, _server, msgArray);

                    //If not in launch process, then we are on client peer and should invoke ServerLaunched.
                    if (!_launchRequest)
                    {
                        ServerLaunched?.Invoke();
                    }
                }
                break;
            }
        }
    }
}
