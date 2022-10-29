using AMLCore.Injection.GSO;
using AMLCore.Injection.Native;
using AMLCore.Misc;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GSPlatformClient
{
    public partial class MainWindow : Form
    {
        private readonly string[] _servers;
        private bool _isRefreshing;

        private readonly ServerInfo[] _serverInfo;
        private readonly RoomList[] _serverRooms;
        private readonly string[] _serverTokens;
        private RoomPanel _connectedRoom;
        private bool _connectionRelayEnabled;

        internal RoomClient Connection { get; private set; }

        public MainWindow(string[] servers)
        {
            InitializeComponent();
            if (LicenseManager.UsageMode != LicenseUsageMode.Designtime)
            {
                panel1.Controls.Clear();
            }

            _servers = servers;
            _serverInfo = new ServerInfo[servers.Length];
            _serverRooms = new RoomList[servers.Length];
            _serverTokens = new string[servers.Length];
        }

        private async void timer1_Tick(object sender, EventArgs e)
        {
            if (_isRefreshing) return;
            _isRefreshing = true;
            try
            {
                await UpdateServerList(clearAll: false);
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private void SetupPeerListPanel(RoomPanel room, string roomId, bool relayEnabled)
        {
            var peersArray = Connection.GetPeers();
            room.CurrentPeerCount = room.IsJoined ? peersArray.Length + 1 : peersArray.Length;
            room.SetupPeers(peersArray, (i, c, pp, isNew) =>
            {
                c.RoomId = roomId;
                c.PeerStatusColor = pp.Connected ? Color.Green : Color.Red;
                c.PeerName = pp.UserName;
                c.PeerEndPoint = pp.EndPoint;
                c.PeerDelay = pp.AverageDelayMs;
                c.PeerConnectivity = pp.Connectivity;
                c.RelayEnabled = relayEnabled;
                c.IsRelay = pp.IsRelay;
                c.CanRelay = pp.CanRelay;
                if (isNew)
                {
                    c.RelayRequest += (id, ep) => Relay(id, ep);
                }
            });
            _connectedRoom = room;
        }

        private static readonly Color[] RoomPanelColors = new[]
        {
            Color.FromArgb(220, 229, 238),
            Color.FromArgb(239, 243, 247),
        };
        private void SetupServerPanel(int serverIndex, ServerPanel p,
            bool notRegistered, ServerInfo info, RoomList rooms, bool isNew, bool userCanJoinRoom)
        {
            bool failed = false;
            if (info == null)
            {
                failed = true;
                info = new ServerInfo()
                {
                    Name = "未知的服务器",
                    OnlineUsers = 0,
                    CanRegister = false,
                    RequiresInvitation = false,
                };
                rooms = null;
            }
            if (rooms == null)
            {
                rooms = new RoomList() { Rooms = new RoomPublicInfo[0], CanAdd = false };
            }

            p.ServerName = info.Name;
            p.ServerImage = info.Image;
            p.ServerNotRegistered = notRegistered;
            p.OnlineUsers = info.OnlineUsers;
            p.ServerDelay = 0; //TODO
            p.ConnectionFailed = failed;
            p.CanRegister = info.CanRegister;
            p.CanAddRoom = rooms.CanAdd && userCanJoinRoom;
            p.RegisterText = info.RequiresInvitation ? "输入邀请码" : "加入服务器";
            if (isNew)
            {
                p.Register += () => Register(serverIndex);
                p.NewRoom += () => NewRoom(serverIndex);
            }

            p.SetupRoomPanels(rooms.Rooms.Where(r => !r.IsJoined || r.Id == Connection?.RoomId), (i, c, r, n) =>
            {
                c.RoomId = r.Id;
                c.BackColor = RoomPanelColors[i % RoomPanelColors.Length];
                c.RoomName = r.Name;
                c.RoomOwner = r.OwnerName;
                c.RoomDescription = r.Description;
                c.RoomImage = r.Image;
                c.MaxPeerCount = r.MaxPeers;
                c.CurrentPeerCount = r.Peers;
                if (r.IsJoined)
                {
                    c.IsJoined = true;
                    c.CanJoin = false;
                    c.CanExit = true;
                    c.IsHost = r.IsHost;
                    c.CanLaunch = r.CanLaunch;
                    if (Connection?.RoomId == r.Id)
                    {
                        SetupPeerListPanel(c, r.Id, _connectionRelayEnabled);
                    }
                    else
                    {
                        c.ResetPeers(Enumerable.Empty<PeerPanel>());
                    }
                }
                else
                {
                    c.IsJoined = false;
                    c.CanJoin = r.CanJoin && userCanJoinRoom;
                    c.CanExit = false;
                    c.IsHost = false;
                    c.CanLaunch = false;
                    c.ResetPeers(Enumerable.Empty<PeerPanel>());
                }
                if (n)
                {
                    c.Join += id => JoinRoom(serverIndex, id);
                    c.Exit += id => ExitRoom();
                    c.Launch += id => LaunchRoom(id);
                }
            });
        }

        private async Task UpdateServerList(bool clearAll)
        {
            //Get info for all servers.
            List<Task> updateTasks = new List<Task>();
            for (int index = 0; index < _servers.Length; ++index)
            {
                var i = index;
                updateTasks.Add(Task.Run(async () =>
                {
                    string token;
                    try
                    {
                        if (!UserTokenStorage.TryGetToken(_servers[i], out _, out token, this))
                        {
                            token = null;
                        }
                        _serverTokens[i] = token;
                    }
                    catch
                    {
                        token = null;
                    }
                    try
                    {
                        var info = await GetHttpResponseAsync(_servers[i] + "/serverInfo.json");
                        _serverInfo[i] = JsonSerialization.Deserialize<ServerInfo>(info);
                    }
                    catch
                    {
                        _serverInfo[i] = null;
                        _serverRooms[i] = null;
                        return;
                    }
                    try
                    {
                        var list = await GetHttpResponseAsync(_servers[i] + "/rooms.json?token=" + token);
                        _serverRooms[i] = JsonSerialization.Deserialize<RoomList>(list);
                    }
                    catch
                    {
                        _serverRooms[i] = null;
                    }
                }));
            }
            await Task.WhenAll(updateTasks);

            //Update the list after the above completes.

            panel1.SuspendLayout();
            try
            {
                if (!clearAll && panel1.Controls.Count == 0)
                {
                    clearAll = true;
                }
                var canJoin = Connection == null;
                if (clearAll)
                {
                    panel1.Controls.Clear();
                    for (int i = 0; i < _servers.Length; ++i)
                    {
                        var p = new ServerPanel();
                        SetupServerPanel(i, p, _serverTokens[i] == null, _serverInfo[i], _serverRooms[i], true, canJoin);
                        p.Dock = DockStyle.Top;
                        panel1.Controls.Add(p);
                    }
                }
                else
                {
                    for (int i = 0; i < _servers.Length; ++i)
                    {
                        var p = (ServerPanel)panel1.Controls[i];
                        SetupServerPanel(i, p, _serverTokens[i] == null, _serverInfo[i], _serverRooms[i], false, canJoin);
                    }
                }
                if (Connection != null)
                {
                    UpdateRoomPeers();
                }
            }
            catch
            {
                panel1.Controls.Clear();
            }
            finally
            {
                panel1.ResumeLayout();
            }
        }

        private sealed class ServerInfo
        {
            public string Name;
            public string Image;
            public int OnlineUsers;
            public bool CanRegister;
            public bool RequiresInvitation;
        }

        private sealed class RoomPublicInfo
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public string OwnerName { get; set; }
            public string Image { get; set; }

            public bool IsJoined { get; set; }
            public bool IsHost { get; set; }
            public int MaxPeers { get; set; }
            public int Peers { get; set; }
            public bool CanJoin { get; set; }
            public bool CanLaunch { get; set; }
        }

        private sealed class RoomList
        {
            public bool CanAdd { get; set; }
            public RoomPublicInfo[] Rooms { get; set; }
        }

        private static Task<string> GetHttpResponseAsync(string url, string method = "GET", string body = null)
        {
            return GetHttpResponseWithStatusCodeAsync(url, method, body).ContinueWith(t => t.Result.Content);
        }

        private struct ResponseResult
        {
            public string Content;
            public HttpStatusCode StatusCode;
        }

        private static Task<ResponseResult> GetHttpResponseWithStatusCodeAsync(string url, string method, string body)
        {
            return Task.Factory.StartNew(() =>
            {
                try
                {
                    var request = (HttpWebRequest)WebRequest.Create(url);
                    request.Method = method;
                    if (body != null)
                    {
                        using (var writer = new StreamWriter(request.GetRequestStream(), Encoding.UTF8))
                        {
                            writer.Write(body);
                        }
                        request.ContentType = "application/json";
                    }
                    using (var response = (HttpWebResponse)request.GetResponse())
                    {
                        using (var reader = new StreamReader(response.GetResponseStream()))
                        {
                            return new ResponseResult()
                            {
                                Content = reader.ReadToEnd(),
                                StatusCode = HttpStatusCode.OK,
                            };
                        }
                    }
                }
                catch (WebException e)
                {
                    return new ResponseResult()
                    {
                        Content = null,
                        StatusCode = ((HttpWebResponse)e.Response).StatusCode,
                    };
                }
            });
        }

        private async void Register(int index)
        {
            if (_serverInfo[index]?.CanRegister ?? false)
            {
                var dialog = new RegisterDialog()
                {
                    InvitationEnabled = _serverInfo[index].RequiresInvitation,
                };
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    await SendRegisterRequest(_servers[index], dialog.Code, dialog.UserName);
                    await UpdateServerList(clearAll: false);
                }
            }
        }

        private sealed class RegisterRequest
        {
            public string Invitation { get; set; }
            public string UserName { get; set; }
        }

        private sealed class RegisterResponse
        {
            public string Token { get; set; }
        }

        private async Task SendRegisterRequest(string server, string invitation, string userName)
        {
            var request = JsonSerialization.Serialize(new RegisterRequest()
            {
                Invitation = invitation ?? string.Empty,
                UserName = userName,
            });
            var response = await GetHttpResponseAsync($"{server}/auth/register.json", "POST", request);
            if (response == null)
            {
                WindowsHelper.MessageBox("注册失败");
                return;
            }
            var token = JsonSerialization.Deserialize<RegisterResponse>(response).Token;
            UserTokenStorage.SetToken(server, userName, token);
        }

        private sealed class NewRoomRequest
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public string Image { get; set; }
        }

        private sealed class RoomLoginInfo
        {
            public string Id { get; set; }
            public ulong LoginToken { get; set; }
            public string UdpServer { get; set; }
            public bool CanRelay { get; set; }
        }

        private async void NewRoom(int index)
        {
            var room = _serverRooms[index];
            if (room?.CanAdd ?? false)
            {
                var token = _serverTokens[index];
                if (token == null)
                {
                    return;
                }

                var dialog = new AddRoomDialog();
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    var request = JsonSerialization.Serialize(new NewRoomRequest()
                    {
                        Name = dialog.RoomName,
                        Description = dialog.RoomDescription,
                        Image = RoomImage.BitmapData,
                    });
                    var response = await GetHttpResponseAsync($"{_servers[index]}/rooms.json?token={token}",
                        "POST", request);
                    if (response == null)
                    {
                        WindowsHelper.MessageBox("服务器拒绝了请求");
                        return;
                    }
                    var roomLogin = JsonSerialization.Deserialize<RoomLoginInfo>(response);
                    InitRoomConnection(index, roomLogin, isHost: true, relayEnabled: roomLogin.CanRelay);
                    await UpdateServerList(clearAll: false);
                }
            }
        }

        private static IPEndPoint ParseIPEndPoint(string str)
        {
            var split = str.Split(':');
            return new IPEndPoint(IPAddress.Parse(split[0]), int.Parse(split[1]));
        }

        private void InitRoomConnection(int serverIndex, RoomLoginInfo login, bool isHost, bool relayEnabled)
        {
            Connection?.Dispose();
            Connection = new RoomClient(serverIndex, ParseIPEndPoint(login.UdpServer), login.Id, login.LoginToken, isHost);
            _connectionRelayEnabled = relayEnabled;
            Connection.ServerTimeOut += WrapToUIThreadCall(ExitRoom);
            Connection.ServerLaunched += WrapToUIThreadCall(ServerLaunch);
            Connection.UpdateRoomPeers += WrapToUIThreadCall(UpdateRoomPeers);
            Connection.Start();
        }

        private Action WrapToUIThreadCall(Action action)
        {
            return () =>
            {
                try
                {
                    Invoke(action);
                }
                catch
                {
                }
            };
        }

        private async void JoinRoom(int serverIndex, string id)
        {
            if (_serverTokens[serverIndex] == null) return;
            var response = await GetHttpResponseAsync(
                $"{_servers[serverIndex]}/rooms/{id}/join.json?token={_serverTokens[serverIndex]}", "POST");
            if (response == null)
            {
                WindowsHelper.MessageBox("连接失败");
                return;
            }
            var joinInfo = JsonSerialization.Deserialize<RoomLoginInfo>(response);
            InitRoomConnection(serverIndex, joinInfo, isHost: false, relayEnabled: joinInfo.CanRelay);
            await UpdateServerList(clearAll: false);
        }

        private async void ExitRoom()
        {
            if (Connection == null) return;
            var serverIndex = Connection.HttpServerIndex;
            var id = Connection.RoomId;
            try
            {
                Connection.Dispose();
            }
            catch
            {
            }
            finally
            {
                Connection = null; //Force set to null.
            }
            try
            {
                //Post exit and ignore response.
                await GetHttpResponseAsync(
                    $"{_servers[serverIndex]}/rooms/{id}/exit.json?token={_serverTokens[serverIndex]}", "POST");
            }
            catch
            {
            }
            await UpdateServerList(clearAll: false);
        }

        private sealed class RelayRequest
        {
            public string PeerEndPoint { get; set; }
        }

        private async void Relay(string roomId, IPEndPoint ep)
        {
            if (Connection?.RoomId != roomId) return;
            if (MessageBox.Show("对这个玩家改用UDP转发吗？\r\n" +
                "UDP转发可以解决少数UDP穿透失败的情况，但是可能会增加延迟。", "GS联机平台",
                MessageBoxButtons.YesNo) != DialogResult.Yes)
            {
                return;
            }

            var si = Connection.HttpServerIndex;
            var request = JsonSerialization.Serialize(new RelayRequest()
            {
                PeerEndPoint = ep.ToString(),
            });
            var response = await GetHttpResponseWithStatusCodeAsync(
                $"{_servers[si]}/rooms/{roomId}/relay.json?token={_serverTokens[si]}", "POST", request);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                WindowsHelper.MessageBox("连接失败");
                await UpdateServerList(clearAll: false);
                return;
            }
            await UpdateServerList(clearAll: false);
        }

        private async void LaunchRoom(string id)
        {
            if (Connection.RoomId != id) return;
            if (await Connection.LaunchRequest())
            {
                //DialogResult = DialogResult.OK;
                PostLaunch();
                //Close();
            }
            else
            {
                WindowsHelper.MessageBox("启动失败");
                Connection?.Dispose();
                Connection = null;
                await UpdateServerList(clearAll: false);
            }
        }

        private void ServerLaunch()
        {
            //DialogResult = DialogResult.OK;
            PostLaunch();
            //Close();
        }

        private void PostLaunch()
        {
            var connection = Connection;
            var Logger = GSPlatformClientEntry.Logger;

            byte[] serverAddr = null;
            int serverPort = 0;
            if (connection.HostEndPoint != null)
            {
                try
                {
                    var ep = connection.HostEndPoint;
                    serverAddr = Encoding.UTF8.GetBytes(ep.Address.ToString() + '\0');
                    serverPort = ep.Port;
                }
                catch (Exception e)
                {
                    Logger.Error($"Error parsing server address: {connection.HostEndPoint}: {e}");
                    return;
                }
            }
            GSPlatformClientEntry._movedSocket = connection.MoveSocket();
            GSPlatformClientEntry._replaceSocket = true;

            if (connection.IsHost)
            {
                var addr = AddressHelper.Code("gso", 0x7220);
                var startServer = (StartServer)Marshal.GetDelegateForFunctionPointer(addr, typeof(StartServer));
                startServer(10800, 3); //Port is ignored.
                Logger.Info("Redirected gso server");
            }
            else
            {
                var addr = AddressHelper.Code("gso", 0x7A00);
                var startClient = (StartClient)Marshal.GetDelegateForFunctionPointer(addr, typeof(StartClient));
                startClient(ref serverAddr[0], serverPort);
                Logger.Info("Redirected gso client");
            }

            Marshal.WriteInt32(AddressHelper.Code("gso", 0x286B4), 1);

            WaitForConnectionReady(connection);
        }

        private void WaitForConnectionReady(RoomClient connection)
        {
            var dialog = new LaunchStatusDialog(connection);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                DialogResult = DialogResult.OK;
            }
            else
            {
                ExitRoom();
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int StartServer(int port, int players);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int StartClient(ref byte address, int port);


        private sealed class PeerInfo
        {
            public string Name;
            public string EndPoint;
            public bool CanRelay;
            public bool IsRelay;
        }

        private bool _isUpdatingPeers;
        private async void UpdateRoomPeers()
        {
            var conn = Connection;
            if (conn == null) return;
            if (_isUpdatingPeers) return;
            _isUpdatingPeers = true;

            try
            {
                var serverIndex = conn.HttpServerIndex;
                var id = conn.RoomId;
                var response = await GetHttpResponseWithStatusCodeAsync(
                    $"{_servers[serverIndex]}/rooms/{id}.json?token={_serverTokens[serverIndex]}",
                    "GET", null);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var peers = JsonSerialization.Deserialize<PeerInfo[]>(response.Content);
                    await conn.UpdatePeers(peers.ToDictionary(p => ParseIPEndPoint(p.EndPoint),
                        p => new ServerPeerState() { UserName = p.Name, IsRelay = p.IsRelay, CanRelay = p.CanRelay }));
                    if (_connectedRoom != null)
                    {
                        SetupPeerListPanel(_connectedRoom, id, _connectionRelayEnabled);
                    }
                }
                else if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    //404 indicates the room is not found. Shoud disconnect.
                    ExitRoom();
                }
                //Ignore other errors. Server ping (Self msg) can also trigger disconnection.
            }
            catch
            {
            }
            _isUpdatingPeers = false;
        }

        private void MainWindow_Shown(object sender, EventArgs e)
        {
            timer1_Tick(null, EventArgs.Empty);
            timer1.Enabled = true;
        }
    }
}
