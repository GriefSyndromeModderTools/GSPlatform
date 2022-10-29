using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GSPlatformBackServer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        public class RegisterRequest
        {
            public string Token { get; set; } = null!;
            public string IP { get; set; } = null!;
            public string UserName { get; set; } = null!;
            public string[] Groups { get; set; } = Array.Empty<string>();
        }

        [HttpPost("register.json")]
        public string Register([FromBody] RegisterRequest request)
        {
            throw new NotImplementedException();
        }

        [HttpGet("status.json")]
        public object Status()
        {
            return new { Status = "OK" };
        }
    }
}
