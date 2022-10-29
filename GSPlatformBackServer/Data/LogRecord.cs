using System.ComponentModel.DataAnnotations;

namespace GSPlatformBackServer.Data
{
    public class LogRecord
    {
        [Key]
        public long LogRecordId { get; set; }

        //Metadata.
        public DateTime Created { get; set; }

        //Request info.
        public string UserToken { get; set; } = null!;
        public string Request { get; set; } = null!;
        public string RequestServer { get; set; } = null!;
        public string RequestClient { get; set; } = null!;

        //Content.
        public string Content { get; set; } = null!;
    }
}
