using GSPlatformBackServer.Data;
using GSPlatformBackServer.Helpers;
using Microsoft.EntityFrameworkCore;

namespace GSPlatformBackServer
{
    public static class CLICommands
    {
        private static readonly string[] DefaultConsoleUserGroups = new[]
        {
            UserGroupNames.CreateRoom,
            UserGroupNames.UseRoom,
        };

        private static string Register(string invitation, string userName)
        {
            using var db = new AppDbContext();
            var token = TokenHelpers.CreateUserToken();
            var user = new User()
            {
                Token = token,
                Invitation = invitation,
                RegisterIP = "console",
                RegisterTime = DateTime.Now,
                UserName = userName,
            };
            db.Users.Add(user);
            foreach (var g in DefaultConsoleUserGroups)
            {
                db.UserGroups.Add(new UserGroup()
                {
                    User = user,
                    GroupName = g,
                });
            }
            db.SaveChanges();
            return token;
        }

        private static void AddInvitation(string code, int count, TimeSpan time)
        {
            using var db = new AppDbContext();
            db.Database.Migrate();
            db.Invitations.Add(new Invitation()
            {
                Code = code,
                Created = DateTime.Now,
                Description = "console",
                Invalidated = false,
                TimeLimit = DateTime.Now + time,
                UseCount = 0,
                UseCountLimit = count,
            });
            db.SaveChanges();
        }

        private static void PrintInvitations()
        {
            using var db = new AppDbContext();
            db.Database.Migrate();
            foreach (var i in db.Invitations)
            {
                Console.WriteLine($"{i.Code}\t{i.Created.ToLocalTime()}\t{i.UseCount}" +
                    $"{(i.UseCountLimit >= 0 ? "/" + i.UseCountLimit : "")}");
            }
        }

        public static bool ProcessArgs(string[] args)
        {
            if (args.Length > 1)
            {
                Console.WriteLine("Invalid command. Use `Help` to get help.");
                return true;
            }
            if (args.Length == 1)
            {
                Dictionary<string, Action<string[]>> actions = null!;
                void Help(string[] a)
                {
                    if (a.Length == 0)
                    {
                        Console.WriteLine("Use `Help` or `Help=<command>` to get help.");
                        Console.WriteLine($"Actions:\n{string.Join("\n", actions.Keys.Select(s => "  " + s))}");
                    }
                    else if (a.Length == 1)
                    {
                        Console.WriteLine(a[0] switch
                        {
                            "Help" => "Help=<command>: Get help on the given command.",
                            "Register" => "Register=<invitation>;<username>: Register a new user.",
                            "PrintLogin" => "PrintLogin=<filename>;<days>: Create a log file with login history of given time (in days).",
                            "PrintBanned" => "PrintBanned=<filename>: Print all banned users/IPs to the given file.",
                            "AddInvitation" => "AddInvitation=<code>;<count_limit or -1>;<time_limit_days>: Add an invitation code.",
                            "InvalidateInvitation" => "InvalidateInvitation=<code>: Invalidate an invitation code.",
                            "PrintInvitations" => "SeeInvitations: Print all invitation codes to stdout.",
                            _ => "Invalid commnad. Use `Help` to get help.",
                        });
                    }
                    else
                    {
                        Console.WriteLine("Invalid command. Use `Help` or `Help=<command>` to get help.");
                    }
                }
                actions = new()
                {
                    { "Help", Help },
                    { "Register", a => Console.WriteLine(Register(a[0], a[1])) },
                    { "PrintLogin", a => throw new NotImplementedException() },
                    { "PrintBanned", a => throw new NotImplementedException() },
                    { "AddInvitation", a => AddInvitation(a[0], int.Parse(a[1]), TimeSpan.Parse(a[2])) },
                    { "InvalidateInvitation", a => throw new NotImplementedException() },
                    { "PrintInvitations", a => PrintInvitations() },
                };
                var split = args[0].Split('=');
                if (split.Length == 1 && actions.TryGetValue(split[0], out var action))
                {
                    action(Array.Empty<string>());
                }
                else if (split.Length == 2 && actions.TryGetValue(split[0], out action))
                {
                    action(split[1].Split(';'));
                }
                else
                {
                    Console.WriteLine("Invalid command. Use `Help` or `Help=<command>` to get help.");
                }
                return true;
            }
            return false;
        }
    }
}
