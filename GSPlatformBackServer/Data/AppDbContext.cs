using Microsoft.EntityFrameworkCore;

namespace GSPlatformBackServer.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Invitation> Invitations => Set<Invitation>();
        public DbSet<User> Users => Set<User>();
        public DbSet<UserGroup> UserGroups => Set<UserGroup>();
        public DbSet<LogRecord> LogRecords => Set<LogRecord>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=app.db");
        }
    }
}
