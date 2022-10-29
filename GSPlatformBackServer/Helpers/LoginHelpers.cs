using GSPlatformBackServer.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace GSPlatformBackServer.Helpers
{
    internal static class LoginHelpers
    {
        private const int CheckTime = 1;
        private const int KeepTime = 10;

        private static readonly ConcurrentRecentList<LoginEntry> _recentUserLogins =
            new(DateTime.Now, TimeSpan.FromMinutes(CheckTime), TimeSpan.FromMinutes(KeepTime));

        private struct LoginEntry
        {
            public int UserId;
            public uint Address;

            public LoginEntry(int userId, uint address)
            {
                UserId = userId;
                Address = address;
            }

            public override bool Equals(object? obj)
            {
                return obj is LoginEntry entry &&
                       UserId == entry.UserId &&
                       Address == entry.Address;
            }

            public override int GetHashCode()
            {
                return UserId ^ (int)Address;
            }
        }

        public static async Task<User?> TryLoginAsync(this ControllerBase controller, string userToken)
        {
            var db = controller.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var u = await db.Users.FirstOrDefaultAsync(u => u.Token == userToken);
            if (u is null)
            {
                await LogHelpers.WriteLogAsync(controller.HttpContext, "Invalid user", userToken);
                return null;
            }
            await PostLoginAsync(controller, userToken, u.UserId);
            return u;
        }

        public static async Task<User> LoginAsync(this ControllerBase controller, string userToken)
        {
            var db = controller.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var u = await db.Users.FirstOrDefaultAsync(u => u.Token == userToken);
            if (u is null)
            {
                await LogHelpers.WriteLogAsync(controller.HttpContext, "Invalid user", userToken);
                throw new HttpResponseException(HttpStatusCode.Unauthorized);
            }
            await PostLoginAsync(controller, userToken, u.UserId);
            return u;
        }

        public static async Task EnsureUserGroup(this ControllerBase controller, User user, string groupName)
        {
            var db = controller.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            if (!await db.UserGroups.AnyAsync(gg => gg.UserId == user.UserId && gg.GroupName == groupName))
            {
                await LogHelpers.WriteLogAsync(controller.HttpContext, "User not in group: " + groupName, user.Token);
                throw new HttpResponseException(HttpStatusCode.Unauthorized);
            }
        }

        public static Task<bool> CheckUserGroupAsync(this ControllerBase controller, User user, string groupName)
        {
            var db = controller.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            return db.UserGroups.AnyAsync(gg => gg.UserId == user.UserId && gg.GroupName == groupName);
        }

        private static Task PostLoginAsync(ControllerBase controller, string userToken, int userId)
        {
            var addrInt = controller.HttpContext.GetIPAddress().GetIPv4AddressNumber();
            if (_recentUserLogins.Add(new(userId, addrInt), DateTime.Now))
            {
                //Console.WriteLine($"New login record: {userId} from {addrInt}. Total {_recentUserLogins.Count} records.");
                return WriteLoginLogAsync(controller.HttpContext, userToken);
            }
            return Task.CompletedTask;
        }

        private static Task WriteLoginLogAsync(HttpContext context, string userToken)
        {
            return LogHelpers.WriteLogAsync(context, "Login", userToken);
        }

        public static int OnlineUserCount => _recentUserLogins.Count;
    }
}
