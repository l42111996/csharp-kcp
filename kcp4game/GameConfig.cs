using System;
using System.Net;
using dotNetty_kcp;

namespace kcp4game
{
    public class GameConfig:ChannelConfig
    {
        private string tcpIp;
        private int tcpPort;

        private EndPoint _remoteEndPoint;

        public GameConfig()
        {
            KcpTag = true;
        }


        public string TcpIp
        {
            get => tcpIp;
            set => tcpIp = value;
        }

        public int TcpPort
        {
            get => tcpPort;
            set => tcpPort = value;
        }

        public EndPoint RemoteEndPoint
        {
            get => _remoteEndPoint;
            set => _remoteEndPoint = value;
        }
    }
}