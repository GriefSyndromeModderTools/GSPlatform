using GSPlatformBackServer.Helpers;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace GSPlatformBackServer.RoomServer
{
    public static class RoomServerState
    {
        public record struct RoomMember(string Name, string EndPoint, bool CanRelay, bool IsRelay);

        //Store a snapshot of status.
        public struct RoomUserStatus
        {
            public bool IsJoined { get; set; }
            public bool IsHost { get; set; }
            public int MaxPeers { get; set; }
            public int Peers { get; set; }
        }

        public sealed class Room
        {
            //Public info.
            public readonly ulong Id;
            public readonly string Name;
            public readonly string Description;
            public string ImageData;

            public readonly string OwnerUserToken;
            public readonly string OwnerUserName;
            private ulong OwnerUdpToken;
            public IPEndPoint? OwnerEndPoint { get; private set; }
            //Notes on OwnerEndPoint:
            //Currently it's set through:
            //  Join set OwnerUdpToken
            //  EnsureUserJoined set OwnerEndPoint
            //This is fine for now, but it does not allow owner to be relayed.
            //So we should forbid owner to use the relay mechanism.

            //Login info.
            public string UdpServer;

            //Joined user list.
            private readonly Dictionary<ulong, (IPEndPoint EndPoint, string UserToken)> _joinedClientsUdp = new();
            private readonly Dictionary<string, (IPAddress Address, ulong UserRoomToken, string UserName)> _joinedClientsHttp = new();
            private readonly ConcurrentDictionary<ulong, DateTime> _lastUdpReceiveTime = new();
            private readonly Dictionary<(IPEndPoint From, IPEndPoint To), IPEndPoint> _relays = new();

            public Room(ulong id, string name, string description, string ownerToken, string ownerName)
            {
                Id = id;
                Name = name;
                Description = description;
                OwnerUserToken = ownerToken;
                OwnerUserName = ownerName;
                UdpServer = ServerInfo.MainRoomServerEndPoint;
                ImageData = string.Empty;
            }

            public override string ToString()
            {
                return $"joined={_joinedClientsUdp.Count};first={_joinedClientsUdp.FirstOrDefault().Value.EndPoint?.ToString() ?? "none"}";
            }

            public ulong Join(IPAddress httpAddress, string userToken, string userName, bool isHost)
            {
                var roomUserToken = TokenHelpers.CreateRoomUserToken();
                lock (this)
                {
                    //Add or update the user room mapping. Pass the test if the user is not in another
                    //room, or if the user was in a room that has been deleted.
                    if (_userRoomMapping.AddOrUpdate(userToken,
                        (k, v) => v,
                        (k, vold, vnew) => _rooms.ContainsKey(vold) ? vold : vnew,
                        Id) != Id)
                    {
                        throw new InvalidApiUsageException("Join another room");
                    }
                    if (!_joinedClientsHttp.TryAdd(userToken, (httpAddress, roomUserToken, userName)))
                    {
                        //Should not happen (caller should call CheckUserCanJoined)
                        throw new InvalidOperationException();
                    }
                    _joinedClientsUdp.TryAdd(roomUserToken, (new(httpAddress, 0), userToken)); //Port initialized later.
                    _lastUdpReceiveTime.TryAdd(roomUserToken, DateTime.Now);
                    _roomsRoomUserTokenMapping.TryAdd(roomUserToken, this);
                    if (isHost)
                    {
                        OwnerUdpToken = roomUserToken;
                    }
                    QueueUpdatesToAllPeersNoLock();
                }
                return roomUserToken;
            }

            private void QueueUpdatesToAllPeersNoLock()
            {
                foreach (var peer in _joinedClientsUdp)
                {
                    if (peer.Value.EndPoint.Port == 0) continue;
                    _pendingRoomUpdates.Enqueue(peer.Value.EndPoint);
                }
            }

            public void Exit(string userToken)
            {
                lock (this)
                {
                    if (_joinedClientsHttp.Remove(userToken, out var httpInfo))
                    {
                        _joinedClientsUdp.Remove(httpInfo.UserRoomToken);
                        _lastUdpReceiveTime.TryRemove(httpInfo.UserRoomToken, out _);
                        _roomsRoomUserTokenMapping.TryRemove(httpInfo.UserRoomToken, out _);

                        QueueUpdatesToAllPeersNoLock();

                        if (userToken == OwnerUserToken)
                        {
                            //RemoveRoom will lock this again (through ClearAllMembers), which is fine.
                            RemoveRoom(Id);
                        }
                    }
                    else
                    {
                        //TODO should log and throw
                    }
                }
            }

            public void Exit(ulong udpToken)
            {
                lock (this)
                {
                    Exit(_joinedClientsUdp[udpToken].UserToken);
                }
            }

            public void Launch()
            {
                RemoveRoom(Id);
            }

            public bool CheckUserCanJoined(string token)
            {
                lock (this)
                {
                    return !_joinedClientsHttp.ContainsKey(token);
                }
            }

            public bool EnsureUserJoined(ulong udpToken, IPEndPoint endPoint)
            {
                lock (this)
                {
                    if (_joinedClientsUdp.TryGetValue(udpToken, out var ep))
                    {
                        if (ep.EndPoint.Port == 0 &&
                            !ep.EndPoint.Address.MapToIPv4().Equals(endPoint.Address.MapToIPv4()) &&
                            _joinedClientsHttp.TryGetValue(ep.UserToken, out var httpInfo))
                        {
                            //User's IP changes between the HTTP and UDP connections. Fix the address.
                            //Note that this only happens when the user first connects (port is 0).
                            ep.EndPoint = new(endPoint.Address, 0);
                            _joinedClientsHttp[ep.UserToken] = (endPoint.Address, httpInfo.UserRoomToken, httpInfo.UserName);
                        }

                        if (ep.EndPoint.Address.MapToIPv4().Equals(endPoint.Address.MapToIPv4()))
                        {
                            if (ep.EndPoint.Port.Equals(endPoint.Port))
                            {
                                return true;
                            }
                            else if (ep.EndPoint.Port == 0)
                            {
                                //First connection. Port not initialized.
                                _joinedClientsUdp[udpToken] = (endPoint, ep.UserToken);

                                foreach (var peer in _joinedClientsUdp)
                                {
                                    if (peer.Value.EndPoint.Port == 0) continue;
                                    _pendingRoomUpdates.Enqueue(peer.Value.EndPoint);
                                }
                                if (udpToken == OwnerUdpToken)
                                {
                                    OwnerEndPoint = endPoint;
                                }

                                QueueUpdatesToAllPeersNoLock();
                                return true;
                            }
                        }
                    }
                }
                if (!IgnoreRoomUserTokenNotFoundError(udpToken))
                {
                    LogHelpers.WriteLog(endPoint, "Not joined", null);
                }
                return false;
            }

            public Task EnsureUserJoinedAsync(string userToken, HttpContext context)
            {
                lock (this)
                {
                    if (_joinedClientsHttp.TryGetValue(userToken, out var info) &&
                        info.Address.MapToIPv4().Equals(context.GetIPAddress().MapToIPv4()))
                    {
                        return Task.CompletedTask;
                    }
                }
                return LogHelpers.WriteLogAsync(context, "Not joined", userToken);
            }

            public List<RoomMember> GetAllMembersForUser(string userTokenSelf, bool roomCanRelay)
            {
                List<RoomMember> ret = new();
                lock (this)
                {
                    if (!_joinedClientsHttp.TryGetValue(userTokenSelf, out var selfHttpInfo) ||
                        !_joinedClientsUdp.TryGetValue(selfHttpInfo.UserRoomToken, out var selfUdpInfo) ||
                        selfUdpInfo.EndPoint.Port == 0)
                    {
                        return ret;
                    }

                    foreach (var v in _joinedClientsUdp.Values)
                    {
                        if (v.EndPoint.Port == 0)
                        {
                            //Udp connection not started.
                            continue;
                        }
                        if (v.UserToken == userTokenSelf) //TODO compare ulong, which is faster
                        {
                            continue;
                        }

                        //The two lists should be synchronized. Just to be sure to avoid exceptions here.
                        if (_joinedClientsHttp.TryGetValue(v.UserToken, out var httpInfo))
                        {
                            bool isRelay = false;
                            var target = v.EndPoint;
                            if (_relays.TryGetValue((selfUdpInfo.EndPoint, v.EndPoint), out var relayServer))
                            {
                                target = relayServer;
                                isRelay = true;
                            }
                            ret.Add(new(httpInfo.UserName, target.ToString(), roomCanRelay && !isRelay, isRelay));
                        }
                    }
                }
                return ret;
            }

            public bool TryFindMemberByUserToken(string userToken, out ulong token, [MaybeNullWhen(false)] out IPEndPoint ep)
            {
                lock (this)
                {
                    foreach (var v in _joinedClientsUdp)
                    {
                        if (v.Value.UserToken.Equals(userToken))
                        {
                            token = v.Key;
                            ep = v.Value.EndPoint;
                            return true;
                        }
                    }
                    token = default;
                    ep = null;
                    return false;
                }
            }

            public bool TryFindMemberByEndPoint(IPEndPoint ep, out ulong token)
            {
                lock (this)
                {
                    foreach (var v in _joinedClientsUdp)
                    {
                        if (v.Value.EndPoint.MappedEquals(ep))
                        {
                            token = v.Key;
                            return true;
                        }
                    }
                    token = default;
                    return false;
                }
            }

            public void SetRelay(string userToken, IPEndPoint otherPeer, IPEndPoint relay)
            {
                lock (this)
                {
                    if (_joinedClientsHttp.TryGetValue(userToken, out var httpInfo) &&
                        _joinedClientsUdp.TryGetValue(httpInfo.UserRoomToken, out var udpInfo))
                    {
                        if (udpInfo.EndPoint.Port == 0) return;

                        //We don't have a dictionary with endpoint as key, so we must go through all peers.
                        //This should be a very rare operation so it's fine.
                        foreach (var v in _joinedClientsUdp.Values)
                        {
                            if (v.EndPoint.MappedEquals(otherPeer))
                            {
                                _relays[(udpInfo.EndPoint, otherPeer)] = relay;
                                _relays[(otherPeer, udpInfo.EndPoint)] = relay;
                                return;
                            }
                        }
                        QueueUpdatesToAllPeersNoLock();
                    }
                }
            }

            public void EnumerateMembers<TArgs>(Action<ulong, IPEndPoint, TArgs> action, TArgs args)
            {
                lock (this)
                {
                    foreach (var (k, v) in _joinedClientsUdp)
                    {
                        if (v.EndPoint.Port == 0) continue;
                        action(k, v.EndPoint, args);
                    }
                }
            }

            public void SendHostEndPointToAllMembers(Socket socket, ref MainRoomServer.Message msg)
            {
                lock (this)
                {
                    foreach (var v in _joinedClientsUdp.Values)
                    {
                        if (v.EndPoint.Port == 0)
                        {
                            //Udp connection not started.
                            continue;
                        }

                        var host = OwnerEndPoint!;
                        if (_relays.TryGetValue((v.EndPoint, host), out var relayServer))
                        {
                            host = relayServer!;
                        }
                        msg.Address = host.Address.GetIPv4AddressNumber();
                        msg.Port = (ushort)host.Port;

                        socket.SendTo(ref msg, v.EndPoint);
                    }
                }
            }

            public void ClearAllMembers()
            {
                lock (this)
                {
                    foreach (var u in _joinedClientsHttp)
                    {
                        _roomsRoomUserTokenMapping.TryRemove(u.Value.UserRoomToken, out _);
                    }
                    _joinedClientsHttp.Clear();
                    _joinedClientsUdp.Clear();
                    _lastUdpReceiveTime.Clear();
                }
            }

            public void PingReceived(ulong udpToken)
            {
                _lastUdpReceiveTime[udpToken] = DateTime.Now;
            }

            public IEnumerable<ulong> GetDeadUsers(DateTime time)
            {
                return _lastUdpReceiveTime.Where(pair => pair.Value < time).Select(pair => pair.Key);
            }

            public void RemoveRoomIfEmpty()
            {
                lock (this)
                {
                    if (_joinedClientsHttp.Count == 0)
                    {
                        //RemoveRoom will lock this again (through ClearAllMembers), which is fine.
                        RemoveRoom(Id);
                    }
                }
            }

            public RoomUserStatus GetUserStatus(string userToken)
            {
                lock (this)
                {
                    return new RoomUserStatus()
                    {
                        IsJoined = _joinedClientsHttp.ContainsKey(userToken),
                        IsHost = OwnerUserToken == userToken,
                        MaxPeers = 3,
                        Peers = _joinedClientsHttp.Count,
                    };
                }
            }
        }

        //Room ID -> Room.
        private static readonly ConcurrentDictionary<ulong, Room> _rooms = new();
        //RoomUserToken -> Room.
        private static readonly ConcurrentDictionary<ulong, Room> _roomsRoomUserTokenMapping = new();
        //Room list. Need to lock before access (only accessed when adding/deleting/GetAllRooms).
        private static readonly List<Room> _roomList = new();
        //List of rooms that need to broadcast RoomEvent messages.
        public static readonly ConcurrentQueue<IPEndPoint> _pendingRoomUpdates = new();

        //Recent removed user tokens.
        private static readonly ConcurrentRecentList<ulong> _recentUserTokens =
            new(DateTime.Now, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));
        private static readonly ConcurrentRecentList<ulong> _recentRoomIds =
            new(DateTime.Now, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));

        private static readonly ConcurrentDictionary<string, ulong> _userRoomMapping = new();

        public static Room[] GetAllRooms()
        {
            lock (_roomList)
            {
                return _roomList.ToArray();
            }
        }

        public static Room AddRoom(string name, string description, string ownerToken, string ownerName)
        {
            var id = TokenHelpers.CreateRoomId();
            var room = new Room(id, name, description, ownerToken, ownerName);
            if (!_rooms.TryAdd(id, room))
            {
                //Internal error.
                throw new InvalidOperationException();
            }
            lock (_roomList)
            {
                _roomList.Add(room);
            }
            return room;
        }

        public static void RemoveRoom(ulong id)
        {
            if (_rooms.TryRemove(id, out var room))
            {
                room.ClearAllMembers();
                lock (_roomList)
                {
                    _roomList.Remove(room);
                }
            }
        }

        public static Room? FindRoomByRoomId(ulong id)
        {
            return _rooms.TryGetValue(id, out var room) ? room : null;
        }

        public static Room? FindRoomByRoomUserToken(ulong id)
        {
            return _roomsRoomUserTokenMapping.TryGetValue(id, out var room) ? room : null;
        }

        public static void RemoveDeadUsers(DateTime time)
        {
            foreach (var room in _rooms.Values)
            {
                foreach (var k in room.GetDeadUsers(time))
                {
                    room.Exit(k);
                }
                room.RemoveRoomIfEmpty();
            }
            foreach (var (k, v) in _userRoomMapping)
            {
                if (!_rooms.ContainsKey(v))
                {
                    _userRoomMapping.TryRemove(k, out _);
                }
            }
        }

        public static bool GetRoomEventBroadcastTask([MaybeNullWhen(false)] out IPEndPoint peer)
        {
            return _pendingRoomUpdates.TryDequeue(out peer);
        }

        public static bool IgnoreRoomNotFoundError(ulong roomId)
        {
            return _recentRoomIds.Contains(roomId, DateTime.Now - TimeSpan.FromSeconds(30));
        }

        public static bool IgnoreRoomUserTokenNotFoundError(ulong token)
        {
            return _recentUserTokens.Contains(token, DateTime.Now - TimeSpan.FromSeconds(30));
        }
    }
}
