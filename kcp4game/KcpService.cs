using System;
using doNetty_tcp.code;
using DotNetty.Buffers;
using dotNetty_kcp;

namespace dotNetty_kcp
{
    public class KcpService<T>:KcpListener
    {
        private IProtoDecodeEncode<T> _protoDecodeEncode;

        private IMessageManager<T> _messageManager;


        public void handleReceive(IByteBuffer byteBuf, Ukcp ukcp,int protocolType)
        {
            var message = _protoDecodeEncode.decode(byteBuf);
            message.ProtocolType = protocolType;
            var handler = _messageManager.getHandler(message.MessageId);
            handler.handler(message);
        }

        public void handleException(Exception ex, Ukcp ukcp)
        {


        }

        public void handleClose(Ukcp ukcp)
        {


        }

    }
}