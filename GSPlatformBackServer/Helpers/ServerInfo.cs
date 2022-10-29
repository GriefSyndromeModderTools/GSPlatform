using System.Net;

namespace GSPlatformBackServer.Helpers
{
    internal static class ServerInfo
    {
        public static string ThisServerName { get; private set; } = "默认服务器";
        public static string UdpServerAddress { get; private set; } = "127.0.0.1";
        public static int MainRoomServerPort { get; private set; } = 10000;
        public static int AuxRoomServerPort { get; private set; } = 10001;
        public static string RelayServerHttp { get; private set; } = "http://127.0.0.1:10002";
        public static string RelayServerAddress { get; private set; } = "127.0.0.1";
        public static bool RegisteredUserCanUseForward { get; private set; } = true;

        public static string MainRoomServerEndPoint => $"{UdpServerAddress}:{MainRoomServerPort}";
        public static string AuxRoomServerEndPoint => $"{UdpServerAddress}:{AuxRoomServerPort}";

        public static string ServerImageData = "";

        public static void LoadSettings(ConfigurationManager config)
        {
            ThisServerName = config.GetSection("RoomServerName").Value;
            UdpServerAddress = config.GetSection("UdpServerAddress").Value;
            MainRoomServerPort = int.Parse(config.GetSection("MainRoomServerPort").Value);
            AuxRoomServerPort = int.Parse(config.GetSection("AuxRoomServerPort").Value);
            RelayServerHttp = config.GetSection("RelayServerHttp").Value;
            RelayServerAddress = config.GetSection("RelayServerAddress").Value;
            RegisteredUserCanUseForward = bool.Parse(config.GetSection("RegisteredUserCanUseForward").Value);

            var img = File.ReadAllBytes("server.png");
            ServerImageData = Convert.ToBase64String(img);
        }
    }
}
