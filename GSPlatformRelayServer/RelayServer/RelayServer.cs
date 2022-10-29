using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace GSPlatformRelayServer.RelayServer
{
    //TODO avoid IPAddress and IPEndPoint related allocations for each message
    public unsafe class RelayServer : UdpServer<RelayServer.Message>
    {
        private sealed class PeerInfo
        {
            public readonly IPAddress ForwardToAddress;
            public readonly ulong ForwardToToken;
            public IPEndPoint? SelfEndPoint;
            public IPEndPoint? ForwardToEndPoint;
            public volatile int LastActive;

            public PeerInfo(IPAddress f, ulong ft)
            {
                ForwardToAddress = f;
                ForwardToToken = ft;
                SetActiveTime();
            }

            public void SetActiveTime()
            {
                LastActive = GetTime();
            }
        }

        private static readonly int ClientTimeOut = 120; //seconds
        private static readonly TimeSpan ClientTimeOutInterval = TimeSpan.FromSeconds(10);
        public static int Port { get; set; }

        private static readonly ConcurrentDictionary<IPEndPoint, PeerInfo> _connections = new();
        private static readonly ConcurrentDictionary<ulong, PeerInfo> _pendingPeers = new();
        private static readonly Stopwatch _time = Stopwatch.StartNew(); //Assuming thread-safety.

        private static int GetTime() => _time.Elapsed.Seconds;

        public RelayServer() : base(Port, 1)
        {
            AddDelayedTask(RemoveDeadConnections);
        }

        public struct Message : IMessageBuffer
        {
            public MessageHeader Header;
            public fixed byte Data[1000];
            public void Init() => Header.Init();
            public bool Check(int size) => true; //Forwarded messages may not be in our format.
            public bool CheckGSP(int size) => Header.Check(size);
        }

        public static void AddForward(IPAddress addr1, ulong token1, IPAddress addr2, ulong token2)
        {
            _pendingPeers.TryAdd(token1, new PeerInfo(addr2, token2));
            _pendingPeers.TryAdd(token2, new PeerInfo(addr1, token1));
        }

        protected override void HandleMessage(Socket socket, IPEndPoint src, ref Message msg, int recvLength)
        {
            if (_connections.TryGetValue(src, out var pi))
            {
                socket.SendTo(ref msg, recvLength, pi.ForwardToEndPoint!);
                pi.SetActiveTime();
            }
            else if (msg.CheckGSP(recvLength) &&
                _pendingPeers.TryGetValue(msg.Header.ClientToken, out pi))
            {
                HandleNewPeer(src, msg.Header.ClientToken, pi);
                pi.SetActiveTime();
            }
        }

        private static void HandleNewPeer(IPEndPoint ep, ulong token, PeerInfo pi)
        {
            pi.SelfEndPoint = ep;
            if (_pendingPeers.TryGetValue(pi.ForwardToToken, out var fpi) &&
                fpi.SelfEndPoint is not null)
            {
                fpi.ForwardToEndPoint = ep;
                pi.ForwardToEndPoint = fpi.SelfEndPoint;
                _pendingPeers.TryRemove(pi.ForwardToToken, out _);
                _pendingPeers.TryRemove(token, out _);
                _connections.TryAdd(ep, pi);
                _connections.TryAdd(fpi.SelfEndPoint, fpi);
            }
        }

        private DateTime _nextRemoveDeadConnections = DateTime.MinValue;

        private void RemoveDeadConnections(int threadId, Socket socket, ref Message buffer)
        {
            if (threadId != 0 || DateTime.Now < _nextRemoveDeadConnections)
            {
                return;
            }
            _nextRemoveDeadConnections = DateTime.Now + ClientTimeOutInterval;
            var t = GetTime();
            foreach (var (k, v) in _pendingPeers)
            {
                if (v.LastActive + ClientTimeOut < t)
                {
                    _pendingPeers.TryRemove(k, out _);
                }
            }
            foreach (var (k, v) in _connections)
            {
                if (v.LastActive + ClientTimeOut < t)
                {
                    _connections.TryRemove(k, out _);
                }
            }
        }
    }
}
