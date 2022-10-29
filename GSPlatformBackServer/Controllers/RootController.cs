using GSPlatformBackServer.Helpers;
using GSPlatformBackServer.RoomServer;
using Microsoft.AspNetCore.Mvc;

namespace GSPlatformBackServer.Controllers
{
    [Route("/")]
    [ApiController]
    public class RootController : Controller
    {
        public sealed record ServerInfoResponse(string Name, int OnlineUsers, string Image,
            bool CanRegister, bool RequiresInvitation);

        [HttpGet("serverInfo.json")]
        public ServerInfoResponse Info()
        {
            return new ServerInfoResponse(ServerInfo.ThisServerName, LoginHelpers.OnlineUserCount, ServerInfo.ServerImageData,
                true, true);
        }

        public sealed record ServerStatusResponse(int roomCount, string roomInfo);

        [HttpGet("status.json")]
        public ServerStatusResponse Status()
        {
            var rooms = RoomServerState.GetAllRooms();
            var info = rooms[0].ToString()!;
            return new(rooms.Length, info);
        }
    }
}
