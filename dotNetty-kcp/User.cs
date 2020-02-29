using System;
using System.Net;
using DotNetty.Transport.Channels;

namespace dotNetty_kcp
{
    public class User
    {

        private IChannel channel;
        private EndPoint remoteAddress;
        private EndPoint localAddress;

        private object Object;


        public User(IChannel channel, EndPoint remoteAddress, EndPoint localAddress)
        {
            this.channel = channel;
            this.remoteAddress = remoteAddress;
            this.localAddress = localAddress;
        }


        public IChannel Channel
        {
            get => channel;
            set => channel = value;
        }


        public EndPoint RemoteAddress
        {
            get => remoteAddress;
            set => remoteAddress = value;
        }

        public EndPoint LocalAddress
        {
            get => localAddress;
            set => localAddress = value;
        }

        public object O
        {
            get => Object;
            set => Object = value;
        }
    }
}