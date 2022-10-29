using System.Net;
using System.Net.Sockets;

namespace GSPlatformBackServer.Helpers
{
    internal static class IPAddressHelpers
    {
        public static uint GetIPv4AddressNumber(this IPAddress addr)
        {
            addr = addr.MapToIPv4();
#pragma warning disable CS0618
            return addr.AddressFamily == AddressFamily.InterNetwork ? (uint)addr.Address : 0xFFFFFFFFu;
#pragma warning restore CS0618
        }

        public static bool MappedEquals(this IPEndPoint ep, IPEndPoint? other)
        {
            if (other is null) return ep is null;
            return ep.Address.MapToIPv4().Equals(other.Address.MapToIPv4()) && ep.Port == other.Port;
        }
    }
}
