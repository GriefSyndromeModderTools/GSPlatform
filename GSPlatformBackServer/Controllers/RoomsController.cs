using GSPlatformBackServer.Data;
using GSPlatformBackServer.Helpers;
using GSPlatformBackServer.RoomServer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace GSPlatformBackServer.Controllers
{
    using Room = RoomServerState.Room;
    using RoomMember = RoomServerState.RoomMember;
    using RoomStatus = RoomServerState.RoomUserStatus;

    [Route("[controller]")]
    [ApiController]
    public class RoomsController : ControllerBase
    {
        public record struct RoomPublicInfo([property: JsonIgnore] Room Room,
            [property: JsonIgnore] RoomStatus Status, [property: JsonIgnore] bool UserCanUseRoom)
        {
            public string Id => Room.Id.ToString("X16");
            public string Name => Room.Name;
            public string Description => Room.Description;
            public string OwnerName => Room.OwnerUserName;
            public string Image => Room.ImageData;

            public bool IsJoined => Status.IsJoined;
            public bool IsHost => Status.IsHost;
            public int MaxPeers => Status.MaxPeers;
            public int Peers => Status.Peers;
            public bool CanJoin => !IsJoined && UserCanUseRoom;
            public bool CanLaunch => IsHost;
        }

        public sealed class RoomList
        {
            public bool CanAdd { get; set; }
            public RoomPublicInfo[] Rooms { get; set; } = null!;
        }

        public record struct RoomLoginInfo([property: JsonIgnore] Room Room, ulong LoginToken, bool CanRelay)
        {
            public string Id => Room.Id.ToString("X16");
            public string UdpServer => Room.UdpServer;
        }

        public record CreateRoomRequest(string Name, string Description, string? Image);

        [Route("/[controller].json")]
        [HttpGet]
        public async Task<RoomList> Index([FromQuery] string token)
        {
            var u = await this.LoginAsync(token);
            var canAdd = await this.CheckUserGroupAsync(u, UserGroupNames.CreateRoom);
            var canUse = await this.CheckUserGroupAsync(u, UserGroupNames.UseRoom);
            var canRelay = await this.CheckUserGroupAsync(u, UserGroupNames.UseForwarding);

            return new RoomList()
            {
                Rooms = RoomServerState.GetAllRooms()
                    .Where(r => r.OwnerEndPoint is not null)
                    .Select(r => new RoomPublicInfo(r, r.GetUserStatus(token), canUse))
                    .ToArray(),
                CanAdd = canAdd,
            };
        }

        [Route("/[controller].json")]
        [HttpPost]
        public async Task<RoomLoginInfo> Create([FromBody] CreateRoomRequest requestData, [FromQuery] string token)
        {
            var u = await this.LoginAsync(token);
            await this.EnsureUserGroup(u, UserGroupNames.CreateRoom);
            var canRelay = await this.CheckUserGroupAsync(u, UserGroupNames.UseForwarding);

            var room = RoomServerState.AddRoom(requestData.Name, requestData.Description, u.Token, u.UserName);
            room.ImageData = requestData.Image ?? string.Empty;
            var roomLoginToken = room.Join(HttpContext.GetIPAddress(), token, u.UserName, isHost: true);
            //Join may fail (if user already joined another room).
            //In this case, the created room will be automatically removed later.

            return new RoomLoginInfo(room, roomLoginToken, canRelay);
        }

        [Route("{id}.json")]
        [HttpPut]
        public void Update(string id)
        {
            throw new HttpResponseException(HttpStatusCode.NotImplemented);
        }

        private static Room FindRoomById(string id)
        {
            try
            {
                var i = Convert.ToUInt64(id, 16);
                return RoomServerState.FindRoomByRoomId(i) ?? throw new HttpResponseException(HttpStatusCode.NotFound);
            }
            catch
            {
                throw new HttpResponseException(HttpStatusCode.NotFound);
            }
        }

        [HttpGet("{id}.json")]
        public async Task<IEnumerable<RoomMember>> Get(string id, [FromQuery] string token)
        {
            var u = await this.LoginAsync(token);
            var room = FindRoomById(id);
            await room.EnsureUserJoinedAsync(token, HttpContext);
            var canRelay = await this.CheckUserGroupAsync(u, UserGroupNames.UseForwarding);

            return room.GetAllMembersForUser(token, canRelay);
        }

        [HttpPost("{id}/join.json")]
        public async Task<RoomLoginInfo> Join(string id, [FromQuery] string token)
        {
            var u = await this.LoginAsync(token);
            await this.EnsureUserGroup(u, UserGroupNames.UseRoom);
            var canRelay = await this.CheckUserGroupAsync(u, UserGroupNames.UseForwarding);
            var room = FindRoomById(id);

            if (room.OwnerEndPoint is null || !room.CheckUserCanJoined(token))
            {
                throw new HttpResponseException(HttpStatusCode.Forbidden);
            }
            var roomLoginToken = room.Join(HttpContext.GetIPAddress(), token, u.UserName, isHost: false);
            return new(room, roomLoginToken, canRelay);
        }

        [HttpPost("{id}/exit.json")]
        public async Task Exit(string id, [FromQuery] string token)
        {
            var u = await this.LoginAsync(token);
            var room = FindRoomById(id);
            await room.EnsureUserJoinedAsync(token, HttpContext);
            room.Exit(token);
        }

        [HttpPost("{id}/transfer.json")]
        public void Transfer(string id)
        {
            throw new HttpResponseException(HttpStatusCode.NotImplemented);
        }

        private static readonly HttpClient _client = new();

        private sealed record ConnectRequest(string IP1, ulong Token1, string IP2, ulong Token2);
        private sealed record ConnectResponse(int Port);

        public sealed record RelayResponse(string PeerEndPoint);

        [HttpPost("{id}/relay.json")]
        public async Task Relay(string id, [FromQuery] string token, [FromBody] RelayResponse relayResponse)
        {
            var u = await this.LoginAsync(token);
            await this.EnsureUserGroup(u, UserGroupNames.UseForwarding);
            var room = FindRoomById(id);

            var peerEP = IPEndPoint.Parse(relayResponse.PeerEndPoint);
            if (!room.TryFindMemberByUserToken(token, out var selfToken, out var selfEP) ||
                selfEP.Port == 0 || peerEP.Port == 0 ||
                !room.TryFindMemberByEndPoint(peerEP, out var peerToken))
            {
                throw new HttpResponseException(HttpStatusCode.NotFound);
            }
            ConnectResponse? relayInfo;
            try
            {
                var response = await _client.PostAsJsonAsync($"{ServerInfo.RelayServerHttp}/connect.json",
                    new ConnectRequest(selfEP.Address.ToString(), selfToken, peerEP.Address.ToString(), peerToken));
                relayInfo = await response.Content.ReadFromJsonAsync<ConnectResponse>();
            }
            catch
            {
                throw new HttpResponseException(HttpStatusCode.InternalServerError);
            }
            var relayServerEP = new IPEndPoint(IPAddress.Parse(ServerInfo.RelayServerAddress), relayInfo!.Port);
            room.SetRelay(token, peerEP, relayServerEP);
        }
    }
}
