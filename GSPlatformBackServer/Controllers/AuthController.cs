using GSPlatformBackServer.Data;
using GSPlatformBackServer.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GSPlatformBackServer.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private static readonly string[] DefaultUserGroups = ServerInfo.RegisteredUserCanUseForward ?
            new[]
            {
                UserGroupNames.CreateRoom,
                UserGroupNames.UseRoom,
                UserGroupNames.UseForwarding,
            } :
            new[]
            {
                UserGroupNames.CreateRoom,
                UserGroupNames.UseRoom,
            };

        private readonly AppDbContext _db;

        public AuthController(AppDbContext db)
        {
            _db = db;
        }

        public sealed class RegisterRequest
        {
            public string Invitation { get; set; } = null!;
            public string UserName { get; set; } = null!;
        }

        public sealed class RegisterResponse
        {
            public string Token { get; init; } = null!;
        }

        [HttpGet("register.json")]
        public string Reg() => "REG";

        [HttpPost("register.json")]
        public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
        {
            var invitation = await _db.Invitations.FirstOrDefaultAsync(ii => ii.Code == request.Invitation);
            if (invitation is null)
            {
                throw new InvalidApiUsageException("No invitation");
            }

            var now = DateTime.UtcNow;
            if (invitation.Invalidated ||
                invitation.UseCountLimit != -1 && invitation.UseCount >= invitation.UseCountLimit ||
                invitation.TimeLimit < now)
            {
                throw new InvalidApiUsageException("Invalid invitation " + invitation.Code);
            }

            var token = TokenHelpers.CreateUserToken();
            var user = new User()
            {
                Invitation = invitation.Code,
                Token = token,
                RegisterTime = now,
                RegisterIP = HttpContext.GetIPAddress().ToString(),
                UserName = request.UserName,
            };
            _db.Users.Add(user);
            invitation.UseCount += 1;

            foreach (var g in DefaultUserGroups)
            {
                _db.UserGroups.Add(new UserGroup()
                {
                    User = user,
                    GroupName = g,
                });
            }

            await _db.SaveChangesAsync();

            return new RegisterResponse
            {
                Token = token,
            };
        }
    }
}
