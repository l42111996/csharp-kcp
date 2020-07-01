using DotNetty.Transport.Channels;
using dotNetty_kcp;
using kcp4game;

namespace doNetty_tcp.code
{
    public class TcpChannelHandler<T> : SimpleChannelInboundHandler<Message<T>>
    {
        private readonly IMessageManager<T> _messageManager;

        public TcpChannelHandler(IMessageManager<T> messageManager)
        {
            _messageManager = messageManager;
        }

        protected override void ChannelRead0(IChannelHandlerContext ctx, Message<T> msg)
        {
            var handler =  _messageManager.getHandler(msg.MessageId);
            // msg.ProtocolType = Ukcp.TCP_PROTOCOL;
            handler.handler(msg);
        }
    }
}