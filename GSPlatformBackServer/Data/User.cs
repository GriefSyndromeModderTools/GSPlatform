namespace GSPlatformBackServer.Data
{
    public class User
    {
        public int UserId { get; set; }
        public string Token { get; set; } = null!;
        public string UserName { get; set; } = null!;

        public DateTime RegisterTime { get; set; }
        public string RegisterIP { get; set; } = null!;
        public string Invitation { get; set; } = null!;
    }
}
