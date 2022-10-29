using System.ComponentModel.DataAnnotations;

namespace GSPlatformBackServer.Data
{
    public class Invitation
    {
        [Key]
        public int InvitationId { get; set; }

        public string Code { get; set; } = null!;
        public DateTime Created { get; set; }
        public string Description { get; set; } = null!;

        public bool Invalidated { get; set; }
        public DateTime TimeLimit { get; set; }
        public int UseCount { get; set; }
        public int UseCountLimit { get; set; }
    }
}
