using System;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using dotNetty_kcp.thread;

namespace dotNetty_kcp
{
    public class ClientChannelHandler:ChannelHandlerAdapter
    {
        private readonly IChannelManager _channelManager;

        private readonly ChannelConfig _channelConfig;


        public ClientChannelHandler(IChannelManager channelManager,ChannelConfig channelConfig)
        {
            this._channelManager = channelManager;
            this._channelConfig = channelConfig;
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            Console.WriteLine(exception);
        }

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            var msg = (DatagramPacket) message;
            var ukcp = _channelManager.get(msg);
            ukcp.read(msg.Content);
        }
    }
}