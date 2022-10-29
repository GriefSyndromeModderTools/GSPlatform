using System.ComponentModel.DataAnnotations.Schema;

namespace GSPlatformBackServer.Data
{
    public class UserGroup
    {
        public int UserGroupId { get; set; }

        [ForeignKey(nameof(User))]
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public string GroupName { get; set; } = null!;
    }
}
