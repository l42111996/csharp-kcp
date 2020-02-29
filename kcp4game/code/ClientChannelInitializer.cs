using System;
using DotNetty.Codecs;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;

namespace doNetty_tcp.code
{
    public class ClientChannelInitializer<T>:ChannelInitializer<TcpSocketChannel>
    {
        private IProtoDecodeEncode<T> _protoDecodeEncode;

        private IMessageManager<T> _messageManager;

        protected override void InitChannel(TcpSocketChannel channel)
        {
            var pipeline = channel.Pipeline;
            pipeline.AddLast("frameDecoder", new LengthFieldBasedFrameDecoder(int.MaxValue, 0, 4));
            pipeline.AddLast("frameDecoder", new LengthFieldPrepender(4, false));

            pipeline.AddLast("byteToMessageDecoder", new TcpProtoDecode<T>(_protoDecodeEncode));
            //encode
            pipeline.AddLast("MessageToByteEncoder",new TcpProtoEncode<T>(_protoDecodeEncode));

            pipeline.AddLast("handler", new TcpChannelHandler<T>(_messageManager));



        }
    }
}