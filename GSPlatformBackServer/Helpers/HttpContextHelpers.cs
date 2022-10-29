using System.Net;

namespace GSPlatformBackServer.Helpers
{
    internal static class HttpContextHelpers
    {
        public static IPAddress GetIPAddress(this HttpContext context)
        {
            return context.Connection.RemoteIpAddress ?? IPAddress.None;
        }

        public static int GetIPPort(this HttpContext context)
        {
            return context.Connection.RemotePort;
        }

        public static IPEndPoint GetIPEndPoint(this HttpContext context)
        {
            return new(context.GetIPAddress(), context.Connection.RemotePort);
        }
    }
}
