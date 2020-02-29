using DotNetty.Buffers;
using DotNetty.Codecs;
using DotNetty.Transport.Channels;
using kcp4game;

namespace doNetty_tcp.code
{
    public class TcpProtoEncode<T> : MessageToByteEncoder<Message<T>>
    {
        private readonly IProtoDecodeEncode<T> _protoDecodeEncode;

        public TcpProtoEncode(IProtoDecodeEncode<T> protoDecodeEncode)
        {
            _protoDecodeEncode = protoDecodeEncode;
        }

        protected override void Encode(IChannelHandlerContext context, Message<T> message, IByteBuffer output)
        {
            _protoDecodeEncode.encode(output, message);
        }
    }
}