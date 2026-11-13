namespace Server.Classes
{
    public class ServerConfig
    {
        public string IpAddress { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 5000;
        public int MaxClients { get; set; } = 2;
        public int TokenLifetime { get; set; } = 120;
        public int HeartbeatInterval { get; set; } = 5;
    }
}
